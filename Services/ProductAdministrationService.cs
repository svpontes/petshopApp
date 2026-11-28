using Microsoft.EntityFrameworkCore;
using PetShopApp.Data;
using PetShopApp.Models;

namespace PetShopApp.Services;

/// <summary>
/// Supplies detached inventory data to the administrator's editing interface.
/// </summary>
public sealed record ProductInventoryData(
    List<Product> Products,
    List<AnimalType> AnimalTypes,
    List<Category> Categories);

/// <summary>
/// Contains validated variant values, never an existing variant or product ID.
/// </summary>
public sealed record ProductVariantDraft(string Label, decimal Price, int StockQuantity = 0);

/// <summary>
/// Validates and authorizes product administration using short-lived contexts.
/// Public catalog queries remain in ProductService.
/// </summary>
public sealed class ProductAdministrationService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    AuthService authService)
{
    /// <summary>
    /// Reads the inventory and lookup lists only for a current administrator.
    /// </summary>
    public async Task<ProductInventoryData> GetInventoryAsync()
    {
        var administrator = await RequireAdministratorAsync();
        await using var db = await dbFactory.CreateDbContextAsync();

        var products = await db.Products.AsNoTracking()
            .Include(product => product.Variants)
            .Include(product => product.ProductCategory)
            .OrderBy(product => product.Name).ThenBy(product => product.Id)
            .ToListAsync();
        var animalTypes = await db.AnimalTypes.AsNoTracking()
            .OrderBy(animal => animal.Name).ThenBy(animal => animal.Id)
            .ToListAsync();
        var categories = await db.Categories.AsNoTracking()
            .OrderBy(category => category.Name).ThenBy(category => category.Id)
            .ToListAsync();

        // Do not return protected data after the account changes during a query.
        await RecheckAdministratorAsync(administrator);
        return new ProductInventoryData(products, animalTypes, categories);
    }

    /// <summary>
    /// Creates or updates allowed product fields and returns the persisted ID.
    /// Submitted navigation properties and variant collections are never attached.
    /// </summary>
    public async Task<int> SaveProductAsync(
        Product input,
        IReadOnlyCollection<ProductVariantDraft>? newVariants = null,
        int? expectedStockQuantity = null)
    {
        var administrator = await RequireAdministratorAsync();
        ArgumentNullException.ThrowIfNull(input);

        // Snapshot form values before awaited queries so later edits cannot bypass validation.
        var draft = new Product
        {
            Id = input.Id,
            Name = input.Name,
            Description = input.Description,
            MoreInformation = input.MoreInformation,
            Price = input.Price,
            StockQuantity = input.StockQuantity,
            AnimalTypeId = input.AnimalTypeId,
            CategoryId = input.CategoryId,
            ImageUrl = input.ImageUrl,
            SecondaryImageUrlsRaw = input.SecondaryImageUrlsRaw,
            AvailableOptionsRaw = input.AvailableOptionsRaw
        };

        // Copy and validate all proposed variants before changing any database rows.
        var variants = (newVariants ?? Array.Empty<ProductVariantDraft>())
            .Select(variant => variant is null
                ? throw new ArgumentException("Enter a valid product option.")
                : ValidateVariant(variant.Label, variant.Price, variant.StockQuantity))
            .ToArray();
        if (variants.Select(variant => variant.Label)
            .Distinct(StringComparer.Ordinal).Count() != variants.Length)
        {
            throw new ArgumentException("Each product option must have a different label.");
        }

        var name = draft.Name?.Trim() ?? "";
        if (name.Length == 0 || name.Length > 200)
            throw new ArgumentException("Enter a product name using no more than 200 characters.");
        if (draft.Id < 0)
            throw new ArgumentException("Select a valid product.");
        if (draft.StockQuantity < 0)
            throw new ArgumentException("Stock quantity cannot be negative.");
        if (draft.Price < 0)
            throw new ArgumentException("Product price cannot be negative.");

        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.AnimalTypes.AnyAsync(animal => animal.Id == draft.AnimalTypeId))
            throw new ArgumentException("Select a valid animal type.");

        // Derive category text from the saved relationship, not a submitted label.
        Category? category = null;
        if (draft.CategoryId is int categoryId)
        {
            category = await db.Categories.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == categoryId);
            if (category is null)
                throw new ArgumentException("Select a valid category.");
        }

        var product = draft.Id == 0
            ? new Product()
            : await db.Products.SingleOrDefaultAsync(item => item.Id == draft.Id)
                ?? throw new ArgumentException("This product no longer exists. Refresh the inventory.");

        // A sale after the edit form was opened must not be overwritten by stale inventory.
        if (draft.Id > 0 && expectedStockQuantity is int expected && product.StockQuantity != expected)
            throw new ArgumentException("Stock changed while this form was open. Refresh the inventory and reopen the product.");

        // Reject duplicates against saved variants when adding options during an update.
        if (draft.Id > 0 && variants.Length > 0)
        {
            var labels = variants.Select(variant => variant.Label).ToArray();
            if (await db.ProductVariants.AnyAsync(variant =>
                variant.ProductId == draft.Id && labels.Contains(variant.Label)))
            {
                throw new ArgumentException("An option with this label already exists for this product.");
            }
        }

        // Copy only editable scalar fields into the entity owned by this context.
        product.Name = name;
        product.Description = draft.Description?.Trim() ?? "";
        product.MoreInformation = draft.MoreInformation?.Trim() ?? "";
        product.Price = draft.Price;
        product.StockQuantity = draft.StockQuantity;
        product.AnimalTypeId = draft.AnimalTypeId;
        product.CategoryId = category?.Id;
        product.Category = category?.Name ?? "";
        product.ImageUrl = string.IsNullOrWhiteSpace(draft.ImageUrl)
            ? "/images/placeholder.png" : draft.ImageUrl.Trim();
        product.SecondaryImageUrlsRaw = draft.SecondaryImageUrlsRaw?.Trim() ?? "";
        product.AvailableOptionsRaw = draft.AvailableOptionsRaw?.Trim() ?? "";

        if (draft.Id == 0)
            db.Products.Add(product);

        // EF assigns the new product ID and saves its variants in the same transaction.
        foreach (var variant in variants)
        {
            db.ProductVariants.Add(new ProductVariant
            {
                Product = product,
                Label = variant.Label,
                Price = variant.Price,
                StockQuantity = variant.StockQuantity
            });
        }

        await RecheckAdministratorAsync(administrator);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ArgumentException("Stock changed while saving. Refresh the inventory and reopen the product.");
        }
        return product.Id;
    }

    /// <summary>
    /// Deletes a confirmed product and lets the mapped cascade remove its variants.
    /// Purchase snapshots in Orders.SerializedItems are not changed or deleted.
    /// </summary>
    public async Task<bool> DeleteProductAsync(int productId)
    {
        var administrator = await RequireAdministratorAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var product = await db.Products.SingleOrDefaultAsync(item => item.Id == productId);
        if (product is null)
            return false;

        await RecheckAdministratorAsync(administrator);
        db.Products.Remove(product);
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Adds a validated variant to a product that still exists.
    /// Existing variant IDs and purchase-time prices remain unchanged.
    /// </summary>
    public async Task AddVariantAsync(int productId, string? label, decimal price, int stockQuantity = 0)
    {
        var administrator = await RequireAdministratorAsync();
        var validated = ValidateVariant(label, price, stockQuantity);
        var cleanLabel = validated.Label;

        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.Products.AnyAsync(product => product.Id == productId))
            throw new ArgumentException("This product no longer exists. Refresh the inventory.");
        if (await db.ProductVariants.AnyAsync(variant =>
                variant.ProductId == productId && variant.Label == cleanLabel))
        {
            throw new ArgumentException("This option already exists for this product.");
        }

        db.ProductVariants.Add(new ProductVariant
        {
            ProductId = productId,
            Label = cleanLabel,
            Price = price,
            StockQuantity = validated.StockQuantity
        });

        await RecheckAdministratorAsync(administrator);
        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Replaces one variant's count only if the administrator still sees its current stock.
    /// This compare-and-update rejects stale forms and sales made during editing.
    /// </summary>
    public async Task UpdateVariantStockAsync(int variantId, int expectedQuantity, int newQuantity)
    {
        var administrator = await RequireAdministratorAsync();
        if (newQuantity < 0 || expectedQuantity < 0)
            throw new ArgumentException("Stock quantity cannot be negative.");
        await using var db = await dbFactory.CreateDbContextAsync();
        await RecheckAdministratorAsync(administrator);
        var affected = await db.ProductVariants
            .Where(variant => variant.Id == variantId && variant.StockQuantity == expectedQuantity)
            .ExecuteUpdateAsync(update => update.SetProperty(variant => variant.StockQuantity, newQuantity));
        if (affected != 1)
            throw new ArgumentException("Stock changed or this option was removed. Refresh the inventory and reopen the product.");
    }

    /// <summary>
    /// Applies the same validation to draft options and immediately saved variants.
    /// </summary>
    private static ProductVariantDraft ValidateVariant(string? label, decimal price, int stockQuantity)
    {
        var cleanLabel = label?.Trim() ?? "";
        if (cleanLabel.Length == 0 || cleanLabel.Length > 100 || price <= 0)
            throw new ArgumentException(
                "Enter an option of up to 100 characters and a price greater than zero.");

        if (stockQuantity < 0)
            throw new ArgumentException("Stock quantity cannot be negative.");
        return new ProductVariantDraft(cleanLabel, price, stockQuantity);
    }

    /// <summary>
    /// Rejects direct service calls from visitors, customers or stale admin sessions.
    /// </summary>
    private async Task<User> RequireAdministratorAsync()
    {
        var administrator = authService.CurrentUser;
        if (administrator is null ||
            !await authService.IsAuthorizedAsync(requireAdmin: true) ||
            !ReferenceEquals(authService.CurrentUser, administrator))
        {
            throw new UnauthorizedAccessException("Administrator access is required.");
        }

        return administrator;
    }

    /// <summary>
    /// Verifies the same administrator again after awaited database operations.
    /// </summary>
    private async Task RecheckAdministratorAsync(User administrator)
    {
        if (!ReferenceEquals(authService.CurrentUser, administrator) ||
            !await authService.IsAuthorizedAsync(requireAdmin: true) ||
            !ReferenceEquals(authService.CurrentUser, administrator))
        {
            throw new UnauthorizedAccessException("Your administrator session has changed.");
        }
    }
}
