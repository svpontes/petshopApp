using System.Text;
using Microsoft.EntityFrameworkCore;
using PetShopApp.Data;
using PetShopApp.Models;

namespace PetShopApp.Services;

/// <summary>Returns detached category names and current product counts to the administration UI.</summary>
public sealed record CategoryInventoryItem(int Id, string Name, int ProductCount);

/// <summary>
/// Owns protected category operations. Short SQLite write transactions serialize name checks
/// and changes, so concurrent administrators cannot create equivalent category names.
/// </summary>
public sealed class CategoryAdministrationService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    AuthService authService)
{
    /// <summary>Lists categories and counts only for the same currently authorized administrator.</summary>
    public async Task<List<CategoryInventoryItem>> GetCategoriesAsync()
    {
        var administrator = await RequireAdministratorAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        var categories = await db.Categories.AsNoTracking()
            .OrderBy(category => category.Name).ThenBy(category => category.Id)
            .Select(category => new CategoryInventoryItem(category.Id, category.Name,
                db.Products.Count(product => product.CategoryId == category.Id)))
            .ToListAsync();
        await RecheckAdministratorAsync(administrator);
        return categories;
    }

    /// <summary>Creates a trimmed name after checking all existing names with Unicode-aware comparison.</summary>
    public async Task<int> CreateCategoryAsync(string? name)
    {
        var administrator = await RequireAdministratorAsync();
        var cleanName = ValidateName(name);
        await using var db = await dbFactory.CreateDbContextAsync();
        // SQLite begins a non-deferred write transaction before the duplicate-name query.
        await using var transaction = await db.Database.BeginTransactionAsync();
        await RejectDuplicateAsync(db, cleanName);
        await RecheckAdministratorAsync(administrator);
        var category = new Category { Name = cleanName };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return category.Id;
    }

    /// <summary>
    /// Renames a category without changing its ID, product relationships, stock or order history.
    /// The legacy product category label is synchronized in the same transaction.
    /// </summary>
    public async Task RenameCategoryAsync(int categoryId, string? name, string expectedName)
    {
        var administrator = await RequireAdministratorAsync();
        var cleanName = ValidateName(name);
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var category = await db.Categories.SingleOrDefaultAsync(item => item.Id == categoryId)
            ?? throw new ArgumentException("This category no longer exists. Refresh the category list.");
        EnsureCurrentName(category, expectedName);
        await RejectDuplicateAsync(db, cleanName, categoryId);
        await RecheckAdministratorAsync(administrator);

        category.Name = cleanName;
        // Update only the compatibility label; never attach or replace submitted product data.
        await db.Products.Where(product => product.CategoryId == categoryId)
            .ExecuteUpdateAsync(update => update.SetProperty(product => product.Category, cleanName));
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    /// <summary>
    /// Deletes only an unused category whose name still matches the user's confirmation.
    /// Concurrent product writes cannot slip between the usage check and the deletion.
    /// </summary>
    public async Task<bool> DeleteCategoryAsync(int categoryId, string expectedName)
    {
        var administrator = await RequireAdministratorAsync();
        await using var db = await dbFactory.CreateDbContextAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var category = await db.Categories.SingleOrDefaultAsync(item => item.Id == categoryId);
        if (category is null)
        {
            await RecheckAdministratorAsync(administrator);
            return false;
        }
        EnsureCurrentName(category, expectedName);
        if (await db.Products.AnyAsync(product => product.CategoryId == categoryId))
            throw new ArgumentException("This category contains products. Reassign those products before deleting it.");
        await RecheckAdministratorAsync(administrator);
        db.Categories.Remove(category);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return true;
    }

    /// <summary>Trims outer whitespace, normalizes equivalent Unicode text and enforces model limits.</summary>
    private static string ValidateName(string? name)
    {
        var cleanName = (name ?? "").Trim().Normalize(NormalizationForm.FormC);
        if (cleanName.Length == 0 || cleanName.Length > 50 || cleanName.Any(char.IsControl))
            throw new ArgumentException("Enter a category name using 1 to 50 characters without line breaks.");
        return cleanName;
    }

    /// <summary>
    /// Compares in .NET rather than SQLite UPPER, preserving accented-name case handling.
    /// This check always executes inside the mutation's SQLite write transaction.
    /// </summary>
    private static async Task RejectDuplicateAsync(ApplicationDbContext db, string name, int? exceptId = null)
    {
        var names = await db.Categories.AsNoTracking().Where(category => exceptId == null || category.Id != exceptId)
            .Select(category => category.Name).ToListAsync();
        var normalized = NameKey(name);
        if (names.Any(existing => NameKey(existing) == normalized))
            throw new ArgumentException("A category with this name already exists.");
    }

    // Preserve intentional internal spacing while comparing outer whitespace, case and Unicode equivalents.
    private static string NameKey(string name) => name.Trim().Normalize(NormalizationForm.FormC).ToUpperInvariant();

    // A renamed category requires a fresh review before either saving or confirming deletion.
    private static void EnsureCurrentName(Category category, string expectedName)
    {
        if (!string.Equals(category.Name, expectedName, StringComparison.Ordinal))
            throw new ArgumentException("This category was renamed by another administrator. Refresh the list and try again.");
    }

    /// <summary>Rejects anonymous callers, customers, revoked roles and stale Identity sessions.</summary>
    private async Task<User> RequireAdministratorAsync()
    {
        var administrator = authService.CurrentUser;
        if (administrator is null || !await authService.IsAuthorizedAsync(requireAdmin: true) ||
            !ReferenceEquals(authService.CurrentUser, administrator))
            throw new UnauthorizedAccessException("Administrator access is required.");
        return administrator;
    }

    /// <summary>Checks the same administrator after awaited database work and before mutations.</summary>
    private async Task RecheckAdministratorAsync(User administrator)
    {
        if (!ReferenceEquals(authService.CurrentUser, administrator) ||
            !await authService.IsAuthorizedAsync(requireAdmin: true) ||
            !ReferenceEquals(authService.CurrentUser, administrator))
            throw new UnauthorizedAccessException("Your administrator session has changed.");
    }
}
