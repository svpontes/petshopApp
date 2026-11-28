using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PetShopApp.Data;
using PetShopApp.Models;
using PetShopApp.Services;

namespace PetShopApp.Tests;

/// <summary>
/// Verifies protected mutations and historical purchase preservation using isolated SQLite.
/// Every test owns a separate in-memory connection and real Identity role memberships.
/// </summary>
public sealed class ProductAdministrationServiceTests
{
    [Theory]
    [InlineData("Anonymous")]
    [InlineData("Customer")]
    [InlineData("Revoked")]
    public async Task EveryOperation_RejectsAnUnauthorizedCaller(string caller)
    {
        await using var setup = await TestInventory.CreateAsync(caller);

        // Service calls must enforce authorization even when no page is involved.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.Service.GetInventoryAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.Service.SaveProductAsync(setup.ValidDraft()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.Service.DeleteProductAsync(setup.ProductId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.Service.AddVariantAsync(setup.ProductId, "11 lb", 30m));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.Service.UpdateVariantStockAsync(1, 0, 5));
        await using var db = await setup.Factory.CreateDbContextAsync();
        Assert.Equal(2, await db.Products.CountAsync());
        Assert.Single(await db.ProductVariants.ToListAsync());
    }

    [Fact]
    public async Task SaveProductAsync_CreatesOnlyAllowedFieldsUsingTheSavedCategory()
    {
        await using var setup = await TestInventory.CreateAsync();
        var input = setup.ValidDraft();
        input.Name = "  New collar  ";
        input.Category = "Untrusted category text";
        input.ImageUrl = " ";
        input.Variants.Add(new ProductVariant { Label = "Injected option", Price = -10m });

        var id = await setup.Service.SaveProductAsync(input);

        await using var db = await setup.Factory.CreateDbContextAsync();
        var saved = await db.Products.Include(product => product.Variants).SingleAsync(product => product.Id == id);
        Assert.Equal("New collar", saved.Name);
        Assert.Equal(2, saved.CategoryId);
        Assert.Equal("Accessories", saved.Category);
        Assert.Equal("/images/placeholder.png", saved.ImageUrl);
        Assert.Empty(saved.Variants);
    }

    [Fact]
    public async Task SaveProductAsync_UpdatesTheProductWithoutChangingPurchaseSnapshots()
    {
        await using var setup = await TestInventory.CreateAsync();
        var input = setup.ValidDraft();
        input.Id = setup.ProductId;
        input.Name = "Renamed food";
        input.Price = 99m;
        input.CategoryId = null;
        input.AvailableOptionsRaw = "S, M";
        input.SecondaryImageUrlsRaw = "/images/one.jpg\n/images/two.jpg";

        Assert.Equal(setup.ProductId, await setup.Service.SaveProductAsync(input));

        await using var db = await setup.Factory.CreateDbContextAsync();
        var saved = await db.Products.Include(product => product.Variants).SingleAsync(product => product.Id == setup.ProductId);
        Assert.Equal("Renamed food", saved.Name);
        Assert.Equal(99m, saved.Price);
        Assert.Null(saved.CategoryId);
        Assert.Equal("", saved.Category);
        Assert.Equal(new[] { "S", "M" }, saved.AvailableSizes);
        Assert.Equal(2, saved.SecondaryImageUrls.Count);
        Assert.Single(saved.Variants);
        Assert.Equal(setup.OriginalSnapshot, (await db.Orders.SingleAsync()).SerializedItems);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("long-name")]
    [InlineData("price")]
    [InlineData("animal")]
    [InlineData("category")]
    [InlineData("negative-id")]
    [InlineData("missing-product")]
    public async Task SaveProductAsync_RejectsInvalidInputWithoutSavingAnything(string invalidField)
    {
        await using var setup = await TestInventory.CreateAsync();
        var input = setup.ValidDraft();
        switch (invalidField)
        {
            case "name": input.Name = "   "; break;
            case "long-name": input.Name = new string('x', 201); break;
            case "price": input.Price = -0.01m; break;
            case "animal": input.AnimalTypeId = int.MaxValue; break;
            case "category": input.CategoryId = int.MaxValue; break;
            case "negative-id": input.Id = -1; break;
            case "missing-product": input.Id = int.MaxValue; break;
        }

        await Assert.ThrowsAsync<ArgumentException>(() => setup.Service.SaveProductAsync(input));

        await using var db = await setup.Factory.CreateDbContextAsync();
        Assert.Equal(2, await db.Products.CountAsync());
        Assert.Equal(setup.OriginalSnapshot, (await db.Orders.SingleAsync()).SerializedItems);
    }

    [Fact]
    public async Task DeleteProductAsync_RemovesVariantsAndPreservesTheCompleteOrder()
    {
        await using var setup = await TestInventory.CreateAsync();

        Assert.True(await setup.Service.DeleteProductAsync(setup.ProductId));

        await using var db = await setup.Factory.CreateDbContextAsync();
        Assert.Null(await db.Products.FindAsync(setup.ProductId));
        Assert.Empty(await db.ProductVariants.Where(variant => variant.ProductId == setup.ProductId).ToListAsync());
        Assert.Single(await db.Products.ToListAsync());
        var order = await db.Orders.SingleAsync();
        Assert.Equal(setup.OriginalSnapshot, order.SerializedItems);
        var item = Assert.Single(order.OrderItems);
        Assert.Equal("Original food", item.ProductName);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(10.80m, item.Price);
        Assert.Equal(21.60m, order.TotalAmount);
    }

    [Fact]
    public async Task DeleteProductAsync_ReturnsFalseWhenAlreadyRemoved()
    {
        await using var setup = await TestInventory.CreateAsync();

        Assert.True(await setup.Service.DeleteProductAsync(setup.ProductId));
        Assert.False(await setup.Service.DeleteProductAsync(setup.ProductId));
    }

    [Fact]
    public async Task AddVariantAsync_TrimsTheLabelAndRejectsDuplicateOptions()
    {
        await using var setup = await TestInventory.CreateAsync();

        await setup.Service.AddVariantAsync(setup.ProductId, " 11 lb ", 30m);
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Service.AddVariantAsync(setup.ProductId, "11 lb", 40m));

        await using var db = await setup.Factory.CreateDbContextAsync();
        Assert.Equal(30m, (await db.ProductVariants.SingleAsync(variant => variant.Label == "11 lb")).Price);
        Assert.Equal(2, await db.ProductVariants.CountAsync());
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("11 lb", 0)]
    [InlineData("11 lb", -1)]
    public async Task AddVariantAsync_RejectsInvalidOptions(string label, int price)
    {
        await using var setup = await TestInventory.CreateAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => setup.Service.AddVariantAsync(setup.ProductId, label, price));
    }

    [Fact]
    public async Task ChangedSecurityStamp_RejectsFurtherMutations()
    {
        await using var setup = await TestInventory.CreateAsync();
        var users = setup.Scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var account = await users.FindByEmailAsync("admin@tests.invalid");
        TestInventory.Ensure(await users.UpdateSecurityStampAsync(account!));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.Service.DeleteProductAsync(setup.ProductId));
    }

    [Fact]
    public async Task GetInventoryAsync_IncludesLookupsAndDetachedVariantData()
    {
        await using var setup = await TestInventory.CreateAsync();

        var inventory = await setup.Service.GetInventoryAsync();

        Assert.Equal(new[] { "Collar", "Original food" }, inventory.Products.Select(product => product.Name));
        Assert.Contains(inventory.Categories, category => category.Name == "Food");
        Assert.Contains(inventory.AnimalTypes, animal => animal.Name == "Dog");
        var food = inventory.Products.Single(product => product.Id == setup.ProductId);
        Assert.Single(food.Variants);
        Assert.Equal("Food", food.ProductCategory?.Name);
    }

    [Fact]
    public async Task SaveProductAsync_CreatesTheProductAndAllDraftVariantsTogether()
    {
        await using var setup = await TestInventory.CreateAsync();
        var drafts = new[]
        {
            new ProductVariantDraft(" 2.2 lb ", 12m),
            new ProductVariantDraft("11 lb", 30m)
        };

        var id = await setup.Service.SaveProductAsync(setup.ValidDraft(), drafts);

        await using var db = await setup.Factory.CreateDbContextAsync();
        var saved = await db.Products.Include(product => product.Variants)
            .SingleAsync(product => product.Id == id);
        Assert.Equal(2, saved.Variants.Count);
        Assert.All(saved.Variants, variant => Assert.Equal(id, variant.ProductId));
        Assert.Contains(saved.Variants, variant => variant.Label == "2.2 lb" && variant.Price == 12m);
        Assert.Contains(saved.Variants, variant => variant.Label == "11 lb" && variant.Price == 30m);
    }

    [Fact]
    public async Task SaveProductAsync_AllowsAnEmptyDraftList()
    {
        await using var setup = await TestInventory.CreateAsync();

        var id = await setup.Service.SaveProductAsync(setup.ValidDraft(), Array.Empty<ProductVariantDraft>());

        await using var db = await setup.Factory.CreateDbContextAsync();
        Assert.Empty(await db.ProductVariants.Where(variant => variant.ProductId == id).ToListAsync());
    }

    [Theory]
    [InlineData("", 12)]
    [InlineData("11 lb", 0)]
    [InlineData("11 lb", -1)]
    public async Task SaveProductAsync_RejectsEveryDraftBeforeCreatingAnyRows(string label, int price)
    {
        await using var setup = await TestInventory.CreateAsync();
        var drafts = new[]
        {
            new ProductVariantDraft("2.2 lb", 12m),
            new ProductVariantDraft(label, price)
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            setup.Service.SaveProductAsync(setup.ValidDraft(), drafts));

        // A valid first option must not leave behind a partially created product.
        await using var db = await setup.Factory.CreateDbContextAsync();
        Assert.Equal(2, await db.Products.CountAsync());
        Assert.Single(await db.ProductVariants.ToListAsync());
    }

    [Fact]
    public async Task SaveProductAsync_RejectsRepeatedDraftLabelsAfterTrimming()
    {
        await using var setup = await TestInventory.CreateAsync();
        var drafts = new[]
        {
            new ProductVariantDraft("2.2 lb", 12m),
            new ProductVariantDraft(" 2.2 lb ", 30m)
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            setup.Service.SaveProductAsync(setup.ValidDraft(), drafts));
    }

    [Fact]
    public async Task SaveProductAsync_AddsAnOptionDuringAnUpdateAndPreservesTheOrder()
    {
        await using var setup = await TestInventory.CreateAsync();
        var input = setup.ValidDraft();
        input.Id = setup.ProductId;

        await setup.Service.SaveProductAsync(input,
            new[] { new ProductVariantDraft("11 lb", 30m) });

        await using var db = await setup.Factory.CreateDbContextAsync();
        Assert.Equal(2, await db.ProductVariants.CountAsync());
        Assert.Equal(setup.OriginalSnapshot, (await db.Orders.SingleAsync()).SerializedItems);
    }

    [Fact]
    public async Task SaveProductAsync_RejectsAnExistingLabelBeforeUpdatingTheProduct()
    {
        await using var setup = await TestInventory.CreateAsync();
        var input = setup.ValidDraft();
        input.Id = setup.ProductId;
        input.Name = "Must not be saved";

        await Assert.ThrowsAsync<ArgumentException>(() =>
            setup.Service.SaveProductAsync(input,
                new[] { new ProductVariantDraft("2.2 lb", 40m) }));

        await using var db = await setup.Factory.CreateDbContextAsync();
        Assert.Equal("Original food", (await db.Products.FindAsync(setup.ProductId))!.Name);
    }

    [Fact]
    public async Task SaveProductAsync_RejectsANullDraftWithoutSavingTheProduct()
    {
        await using var setup = await TestInventory.CreateAsync();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            setup.Service.SaveProductAsync(setup.ValidDraft(),
                new ProductVariantDraft[] { null! }));
    }

    /// <summary>Persists initial product and variant stock through the authorized service.</summary>
    [Fact]
    public async Task SaveProductAsync_PersistsProductAndVariantInventory()
    {
        await using var setup = await TestInventory.CreateAsync();
        var draft = setup.ValidDraft();
        draft.StockQuantity = 8;
        var id = await setup.Service.SaveProductAsync(draft, [new("11 lb", 30m, 4)]);
        await using var db = await setup.Factory.CreateDbContextAsync();
        Assert.Equal(8, (await db.Products.FindAsync(id))!.StockQuantity);
        Assert.Equal(4, (await db.ProductVariants.SingleAsync(variant => variant.ProductId == id)).StockQuantity);
    }

    /// <summary>Negative inventory is rejected before any product or variant row is saved.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveProductAsync_RejectsNegativeInventory(bool variantStock)
    {
        await using var setup = await TestInventory.CreateAsync();
        var draft = setup.ValidDraft();
        draft.StockQuantity = variantStock ? 0 : -1;
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Service.SaveProductAsync(draft,
            variantStock ? [new ProductVariantDraft("11 lb", 30m, -1)] : null));
        await using var db = await setup.Factory.CreateDbContextAsync();
        Assert.Equal(2, await db.Products.CountAsync());
    }

    /// <summary>A sale made after opening an edit form must not be undone by saving that form.</summary>
    [Fact]
    public async Task SaveProductAsync_RejectsAStaleStockCount()
    {
        await using var setup = await TestInventory.CreateAsync();
        var draft = setup.ValidDraft();
        draft.Id = setup.ProductId;
        draft.StockQuantity = 10;
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Service.SaveProductAsync(draft, expectedStockQuantity: 10));
        await using var db = await setup.Factory.CreateDbContextAsync();
        Assert.Equal(0, (await db.Products.FindAsync(setup.ProductId))!.StockQuantity);
    }

    /// <summary>Variant replenishment uses compare-and-update and blocks stale or negative values.</summary>
    [Fact]
    public async Task UpdateVariantStockAsync_ReplenishesAndRejectsStaleOrNegativeCounts()
    {
        await using var setup = await TestInventory.CreateAsync();
        await using var db = await setup.Factory.CreateDbContextAsync();
        var variant = await db.ProductVariants.AsNoTracking().SingleAsync();
        await setup.Service.UpdateVariantStockAsync(variant.Id, 0, 8);
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Service.UpdateVariantStockAsync(variant.Id, 0, 20));
        await Assert.ThrowsAsync<ArgumentException>(() => setup.Service.UpdateVariantStockAsync(variant.Id, 8, -1));
        Assert.Equal(8, (await db.ProductVariants.AsNoTracking().SingleAsync()).StockQuantity);
    }

    /// <summary>
    /// Builds real Identity services and a fresh SQLite model without using local files.
    /// </summary>
    private sealed class TestInventory : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ServiceProvider provider;
        public AsyncServiceScope Scope { get; }
        public IDbContextFactory<ApplicationDbContext> Factory => Scope.ServiceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        public ProductAdministrationService Service => Scope.ServiceProvider.GetRequiredService<ProductAdministrationService>();
        public int ProductId { get; private set; }
        public string OriginalSnapshot { get; private set; } = "";

        private TestInventory(SqliteConnection connection, ServiceProvider provider)
        {
            this.connection = connection;
            this.provider = provider;
            Scope = provider.CreateAsyncScope();
        }

        public static async Task<TestInventory> CreateAsync(string caller = "Admin")
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<User>().AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddScoped<AuthService>();
            services.AddScoped<ProductAdministrationService>();
            var setup = new TestInventory(connection, services.BuildServiceProvider());
            var db = setup.Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            await IdentityDataInitializer.InitializeAsync(setup.Scope.ServiceProvider);

            var users = setup.Scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = new User { Name = "Inventory test", UserName = "admin@tests.invalid", Email = "admin@tests.invalid" };
            // This password exists only in a disposable in-memory test database.
            Ensure(await users.CreateAsync(user, "Inventory#2026Test"));
            Ensure(await users.AddToRoleAsync(user, caller == "Customer" ? "Customer" : "Admin"));

            var product = new Product
            {
                Name = "Original food", Price = 12m, AnimalTypeId = 1,
                CategoryId = 1, Category = "Food",
                Variants = [new ProductVariant { Label = "2.2 lb", Price = 12m }]
            };
            db.Products.AddRange(product, new Product { Name = "Collar", Price = 10m, AnimalTypeId = 2, CategoryId = 2 });
            await db.SaveChangesAsync();
            setup.ProductId = product.Id;
            var order = new Order
            {
                UserId = user.Id, TotalAmount = 21.60m,
                OrderItems = [new OrderItem { ProductId = product.Id, ProductName = product.Name, Size = "2.2 lb", Quantity = 2, Price = 10.80m }]
            };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            setup.OriginalSnapshot = order.SerializedItems;

            if (caller != "Anonymous")
            {
                var claims = setup.Scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<User>>();
                await setup.Scope.ServiceProvider.GetRequiredService<AuthService>().InitializeAsync(await claims.CreateAsync(user));
            }
            if (caller == "Revoked")
                Ensure(await users.RemoveFromRoleAsync(user, "Admin"));

            return setup;
        }

        // A valid detached draft must be checked again by the administration service.
        public Product ValidDraft() => new()
        {
            Name = "New product", Price = 10m, AnimalTypeId = 1, CategoryId = 2
        };

        public static void Ensure(IdentityResult result) =>
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));

        public async ValueTask DisposeAsync()
        {
            await Scope.DisposeAsync();
            await provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
