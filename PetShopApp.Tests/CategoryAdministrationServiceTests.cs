using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using PetShopApp.Components.Shared;
using PetShopApp.Data;
using PetShopApp.Models;
using PetShopApp.Services;

namespace PetShopApp.Tests;

/// <summary>
/// Exercises the real category service against isolated file-backed SQLite databases.
/// Identity roles, transactions, catalog queries and historical order snapshots are real.
/// </summary>
public sealed class CategoryAdministrationServiceTests
{
    /// <summary>Page guards cannot substitute for authorization on every public service operation.</summary>
    [Theory]
    [InlineData("Anonymous")]
    [InlineData("Customer")]
    [InlineData("Revoked")]
    public async Task EveryOperation_RejectsUnauthorizedCallers(string caller)
    {
        await using var setup = await CategoryDatabase.CreateAsync(caller);
        await using var scope = await setup.ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetCategoriesAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreateCategoryAsync("New name"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.RenameCategoryAsync(1, "New name", "Food"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteCategoryAsync(3, "Toys"));
        await using var db = setup.NewContext();
        Assert.Equal(3, await db.Categories.CountAsync());
    }

    /// <summary>Counts reflect the category relationship and include unused categories.</summary>
    [Fact]
    public async Task GetCategoriesAsync_ReturnsDetachedNamesAndCurrentCounts()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var categories = await scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>().GetCategoriesAsync();
        Assert.Equal(new[] { "Accessories", "Food", "Toys" }, categories.Select(category => category.Name));
        Assert.Equal(1, categories.Single(category => category.Id == 1).ProductCount);
        Assert.Equal(1, categories.Single(category => category.Id == 2).ProductCount);
        Assert.Equal(0, categories.Single(category => category.Id == 3).ProductCount);
    }

    /// <summary>A newly saved category must be visible through a fresh catalog-service context.</summary>
    [Fact]
    public async Task CreateCategoryAsync_TrimsAndPersistsTheNameForCatalogAndProductForms()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
        var id = await service.CreateCategoryAsync("  Grooming  ");
        var catalog = await scope.ServiceProvider.GetRequiredService<ProductService>().GetCategoriesAsync();
        Assert.Contains(catalog, category => category.Id == id && category.Name == "Grooming");
        await using var db = setup.NewContext();
        Assert.Equal("Grooming", (await db.Categories.FindAsync(id))!.Name);
    }

    /// <summary>Invalid input must not create partially saved rows or erase entered values in the caller.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    [InlineData("long")]
    [InlineData("Bad\nname")]
    public async Task CreateCategoryAsync_RejectsInvalidNames(string? input)
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCategoryAsync(input == "long" ? new string('x', 51) : input));
        await using var db = setup.NewContext();
        Assert.Equal(3, await db.Categories.CountAsync());
    }

    /// <summary>Name uniqueness ignores case and surrounding Unicode whitespace.</summary>
    [Theory]
    [InlineData("food")]
    [InlineData(" FOOD ")]
    [InlineData("FoOd")]
    [InlineData("\u00A0food\u00A0")]
    public async Task CreateCategoryAsync_RejectsEquivalentExistingNames(string name)
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCategoryAsync(name));
    }

    /// <summary>Accents and equivalent composed/decomposed text must not bypass duplicate checks.</summary>
    [Theory]
    [InlineData("CAFÉ")]
    [InlineData("Cafe\u0301")]
    public async Task CreateCategoryAsync_RejectsEquivalentAccentedNames(string duplicate)
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
        await service.CreateCategoryAsync("Café");
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCategoryAsync(duplicate));
    }

    /// <summary>Renaming updates only the category and compatibility label, preserving purchased values.</summary>
    [Fact]
    public async Task RenameCategoryAsync_PreservesRelationshipsInventoryVariantsAndOrderSnapshots()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
        await service.RenameCategoryAsync(1, "  Nutrition  ", "Food");
        var products = await scope.ServiceProvider.GetRequiredService<ProductService>().GetCatalogProductsAsync(1);
        var product = Assert.Single(products);
        Assert.Equal(setup.FoodId, product.Id);
        Assert.Equal(1, product.CategoryId);
        Assert.Equal("Nutrition", product.Category);
        Assert.Equal("Nutrition", product.ProductCategory!.Name);
        Assert.Equal(12m, product.Price);
        Assert.Equal(0, product.StockQuantity);
        Assert.Equal(4, Assert.Single(product.Variants).StockQuantity);
        await using var db = setup.NewContext();
        var order = await db.Orders.SingleAsync();
        Assert.Equal(setup.OrderSnapshot, order.SerializedItems);
        Assert.Equal(21.60m, order.TotalAmount);
    }

    /// <summary>The category may change its own capitalization, but cannot take another category's name.</summary>
    [Fact]
    public async Task RenameCategoryAsync_RejectsDuplicatesAndAllowsItsOwnCaseChange()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
        await Assert.ThrowsAsync<ArgumentException>(() => service.RenameCategoryAsync(1, " accessories ", "Food"));
        await service.RenameCategoryAsync(1, "FOOD", "Food");
        await using var db = setup.NewContext();
        Assert.Equal("FOOD", (await db.Categories.FindAsync(1))!.Name);
        Assert.Equal("FOOD", (await db.Products.FindAsync(setup.FoodId))!.Category);
    }

    /// <summary>A second administrator's rename requires the first to reload before saving or deleting.</summary>
    [Fact]
    public async Task StaleNames_CannotOverwriteOrDeleteARenamedCategory()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
        await service.RenameCategoryAsync(3, "Play", "Toys");
        await Assert.ThrowsAsync<ArgumentException>(() => service.RenameCategoryAsync(3, "Other", "Toys"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteCategoryAsync(3, "Toys"));
        await using var db = setup.NewContext();
        Assert.Equal("Play", (await db.Categories.FindAsync(3))!.Name);
    }

    /// <summary>Deletion must never detach a product, even when bypassing the disabled UI button.</summary>
    [Fact]
    public async Task DeleteCategoryAsync_RejectsUsedCategoriesAndPreservesEveryProduct()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteCategoryAsync(1, "Food"));
        Assert.Contains("contains products", error.Message);
        await using var db = setup.NewContext();
        Assert.NotNull(await db.Categories.FindAsync(1));
        Assert.Equal(1, (await db.Products.FindAsync(setup.FoodId))!.CategoryId);
        Assert.Equal(setup.OrderSnapshot, (await db.Orders.SingleAsync()).SerializedItems);
    }

    /// <summary>A category assigned after the confirmation opened must also be protected.</summary>
    [Fact]
    public async Task DeleteCategoryAsync_RechecksUsageAfterTheDisplayedListBecomesStale()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
        Assert.Equal(0, (await service.GetCategoriesAsync()).Single(category => category.Id == 3).ProductCount);
        await using var db = setup.NewContext();
        await db.Products.Where(product => product.Id == setup.FoodId)
            .ExecuteUpdateAsync(update => update.SetProperty(product => product.CategoryId, 3));
        await Assert.ThrowsAsync<ArgumentException>(() => service.DeleteCategoryAsync(3, "Toys"));
        Assert.NotNull(await db.Categories.FindAsync(3));
    }

    /// <summary>Unused categories disappear from fresh lookup lists; repeated deletion is harmless.</summary>
    [Fact]
    public async Task DeleteCategoryAsync_RemovesOnlyUnusedCategoriesAndReturnsFalseIfMissing()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
        Assert.True(await service.DeleteCategoryAsync(3, "Toys"));
        Assert.False(await service.DeleteCategoryAsync(3, "Toys"));
        var catalog = await scope.ServiceProvider.GetRequiredService<ProductService>().GetCategoriesAsync();
        Assert.DoesNotContain(catalog, category => category.Id == 3);
        await using var db = setup.NewContext();
        Assert.Equal(2, await db.Products.CountAsync());
        Assert.Equal(setup.OrderSnapshot, (await db.Orders.SingleAsync()).SerializedItems);
    }

    /// <summary>Missing category IDs must not be recreated by a rename request.</summary>
    [Fact]
    public async Task RenameCategoryAsync_RejectsMissingCategories()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>()
            .RenameCategoryAsync(int.MaxValue, "Missing", "Original"));
    }

    /// <summary>A security-stamp change immediately revokes already initialized category services.</summary>
    [Fact]
    public async Task ChangedSecurityStamp_RejectsFurtherCategoryMutations()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var admin = (await users.FindByEmailAsync("category-admin@example.invalid"))!;
        Assert.True((await users.UpdateSecurityStampAsync(admin)).Succeeded);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>()
            .CreateCategoryAsync("Rejected"));
    }

    /// <summary>Two independent administrator scopes cannot create duplicate names concurrently.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCategoryWrites_RejectEquivalentNames(bool rename)
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        using var start = new Barrier(2);
        async Task<bool> SaveAsync(bool second)
        {
            await using var scope = await setup.ScopeAsync(second: second);
            var service = scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>();
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(20)));
            try
            {
                if (rename) await service.RenameCategoryAsync(second ? 3 : 1, second ? "SHARED" : "Shared", second ? "Toys" : "Food");
                else await service.CreateCategoryAsync(second ? " SHARED " : "Shared");
                return true;
            }
            catch (ArgumentException) { return false; }
        }
        var saved = await Task.WhenAll(Task.Run(() => SaveAsync(false)), Task.Run(() => SaveAsync(true)));
        Assert.Single(saved, success => success);
        await using var db = setup.NewContext();
        Assert.Single(await db.Categories.ToListAsync(), category => category.Name.Trim().ToUpperInvariant() == "SHARED");
    }

    /// <summary>Static rendering must show the inline manager and disable deletion of used categories.</summary>
    [Fact]
    public async Task CategoryManager_RendersCategoryNamesCountsAndConfirmationMarkup()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider, scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<CategoryManager>();
            return component.ToHtmlString();
        });
        Assert.Contains("Product Categories", html);
        Assert.Contains("Food", html);
        Assert.Contains("Reassign linked products", html);
        Assert.Contains("<dialog", html);
        Assert.Contains("Delete category?", html);
        Assert.Contains("New category name", html);
    }

    /// <summary>A failed category save rolls back its compatibility labels as well as the name.</summary>
    [Fact]
    public async Task RenameCategoryAsync_RollsBackProductLabelsWhenCategorySavingFails()
    {
        await using var setup = await CategoryDatabase.CreateAsync();
        await using var scope = await setup.ScopeAsync();
        await using var db = setup.NewContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectRename BEFORE UPDATE ON Categories WHEN NEW.Name = 'Rejected' BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<CategoryAdministrationService>()
            .RenameCategoryAsync(1, "Rejected", "Food"));
        Assert.Equal("Food", (await db.Categories.FindAsync(1))!.Name);
        Assert.Equal("Food", (await db.Products.FindAsync(setup.FoodId))!.Category);
        Assert.Equal(setup.OrderSnapshot, (await db.Orders.SingleAsync()).SerializedItems);
    }

    /// <summary>Provides independent database connections and real Identity principals for each caller.</summary>
    private sealed class CategoryDatabase : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"petshop-categories-{Guid.NewGuid():N}.db");
        private readonly DbContextOptions<ApplicationDbContext> options;
        private readonly string caller;
        private int adminId;
        private int otherAdminId;
        public ServiceProvider Provider { get; }
        public int FoodId { get; private set; }
        public string OrderSnapshot { get; private set; } = "";

        private CategoryDatabase(string caller)
        {
            this.caller = caller;
            var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 15 }.ToString();
            options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContextFactory<ApplicationDbContext>(builder => builder.UseSqlite(connectionString));
            services.AddIdentityCore<User>().AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddScoped<AuthService>();
            services.AddScoped<ProductService>();
            services.AddScoped<CategoryAdministrationService>();
            services.AddSingleton<NavigationManager, TestNavigationManager>();
            services.AddSingleton<IJSRuntime, NoJavaScriptRuntime>();
            Provider = services.BuildServiceProvider();
        }
        public ApplicationDbContext NewContext() => new(options);

        public static async Task<CategoryDatabase> CreateAsync(string caller = "Admin")
        {
            var setup = new CategoryDatabase(caller);
            await using var scope = setup.Provider.CreateAsyncScope();
            await using var db = setup.NewContext();
            await db.Database.EnsureCreatedAsync();
            await IdentityDataInitializer.InitializeAsync(scope.ServiceProvider);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var admin = new User { Name = "Category administrator", Email = "category-admin@example.invalid", UserName = "category-admin@example.invalid" };
            Assert.True((await users.CreateAsync(admin, "Category#2026Test")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, caller == "Customer" ? "Customer" : "Admin")).Succeeded);
            setup.adminId = admin.Id;
            var other = new User { Name = "Other administrator", Email = "other-admin@example.invalid", UserName = "other-admin@example.invalid" };
            Assert.True((await users.CreateAsync(other, "Category#2026Test")).Succeeded);
            Assert.True((await users.AddToRoleAsync(other, "Admin")).Succeeded);
            setup.otherAdminId = other.Id;
            var food = new Product { Name = "Original food", AnimalTypeId = 1, CategoryId = 1, Category = "Food", Price = 12m,
                Variants = [new() { Label = "2.2 lb", Price = 12m, StockQuantity = 4 }] };
            db.Products.AddRange(food, new Product { Name = "Collar", AnimalTypeId = 2, CategoryId = 2, Category = "Accessories", Price = 10m, StockQuantity = 3 });
            await db.SaveChangesAsync();
            setup.FoodId = food.Id;
            var order = new Order { UserId = admin.Id, TotalAmount = 21.60m,
                OrderItems = [new() { ProductId = food.Id, ProductName = food.Name, Size = "2.2 lb", Quantity = 2, Price = 10.80m }] };
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            setup.OrderSnapshot = order.SerializedItems;
            return setup;
        }

        public async Task<AsyncServiceScope> ScopeAsync(bool second = false)
        {
            var scope = Provider.CreateAsyncScope();
            if (caller == "Anonymous") return scope;
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var account = (await users.FindByIdAsync((second ? otherAdminId : adminId).ToString()))!;
            var principal = await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<User>>().CreateAsync(account);
            await scope.ServiceProvider.GetRequiredService<AuthService>().InitializeAsync(principal);
            if (caller == "Revoked") Assert.True((await users.RemoveFromRoleAsync(account, "Admin")).Succeeded);
            return scope;
        }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                if (File.Exists(file)) File.Delete(file);
        }
    }

    // Static rendering must never attempt browser interop or navigation to an external service.
    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("http://localhost/", "http://localhost/admin/products");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }
    private sealed class NoJavaScriptRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromException<TValue>(new InvalidOperationException("Unexpected JavaScript during static rendering."));
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
