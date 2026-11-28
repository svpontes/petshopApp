using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PetShopApp.Data;
using PetShopApp.Models;
using PetShopApp.Services;

namespace PetShopApp.Tests;

/// <summary>
/// Exercises real SQLite queries in a separate in-memory database for every test.
/// These tests do not open application databases or run the application seeder.
/// </summary>
public sealed class ProductServiceTests
{
    [Fact]
    public async Task GetCategoriesAsync_ReturnsAlphabeticalCategories()
    {
        await using var database = await TestDatabase.CreateAsync();

        // The context model supplies these three lookup categories.
        var categories = await database.Service.GetCategoriesAsync();

        Assert.Equal(
            new[] { "Accessories", "Food", "Toys" },
            categories.Select(category => category.Name));
    }

    [Fact]
    public async Task GetCatalogProductsAsync_IncludesUncategorizedProductsWhenUnfiltered()
    {
        await using var database = await TestDatabase.CreateAsync();

        var products = await database.Service.GetCatalogProductsAsync();

        Assert.Equal(
            new[] { "Alpha Food", "Beta Collar", "Delta Unassigned", "Gamma Food" },
            products.Select(product => product.Name));
        Assert.Contains(products, product => product.CategoryId is null);

        // Catalog reads return regular prices; the promotion is calculated elsewhere.
        Assert.Equal(10m, products[0].Price);
    }

    [Fact]
    public async Task GetCatalogProductsAsync_FiltersByRelationshipInsteadOfLegacyText()
    {
        await using var database = await TestDatabase.CreateAsync();

        var products = await database.Service.GetCatalogProductsAsync(categoryId: 1);

        Assert.Equal(new[] { "Alpha Food", "Gamma Food" },
            products.Select(product => product.Name));
        Assert.All(products, product => Assert.Equal(1, product.CategoryId));

        // The collar deliberately has legacy text "Food", but belongs to Accessories.
        Assert.DoesNotContain(products, product => product.Name == "Beta Collar");
        Assert.All(products, product => Assert.Equal("Food", product.ProductCategory?.Name));
    }

    [Fact]
    public async Task GetCatalogProductsAsync_ReturnsEmptyWhenNoProductsMatch()
    {
        await using var database = await TestDatabase.CreateAsync();

        // Toys exists but has no products; the second category does not exist.
        Assert.Empty(await database.Service.GetCatalogProductsAsync(categoryId: 3));
        Assert.Empty(await database.Service.GetCatalogProductsAsync(categoryId: int.MaxValue));
    }

    [Fact]
    public async Task GetProductByIdAsync_LoadsCategoryAndVariants()
    {
        await using var database = await TestDatabase.CreateAsync();
        var catalog = await database.Service.GetCatalogProductsAsync(categoryId: 1);
        var productId = catalog.Single(product => product.Name == "Alpha Food").Id;

        // The related entities must remain available after the service context is disposed.
        var product = await database.Service.GetProductByIdAsync(productId);

        Assert.NotNull(product);
        Assert.Equal("Food", product.ProductCategory?.Name);
        Assert.Equal(2, product.Variants.Count);
        Assert.Contains(product.Variants,
            variant => variant.Label == "2.2 lb" && variant.Price == 12m);
    }

    [Fact]
    public async Task GetProductByIdAsync_ReturnsNullForAMissingProduct()
    {
        await using var database = await TestDatabase.CreateAsync();

        Assert.Null(await database.Service.GetProductByIdAsync(int.MaxValue));
    }

    [Fact]
    public async Task EmptyCatalog_ReturnsEmptyCollectionsAndNoProduct()
    {
        await using var database = await TestDatabase.CreateAsync(seedProducts: false);

        // Remove lookup categories to exercise a catalog with no category records either.
        await using (var db = database.Factory.CreateDbContext())
        {
            db.Categories.RemoveRange(await db.Categories.ToListAsync());
            await db.SaveChangesAsync();
        }

        Assert.Empty(await database.Service.GetCategoriesAsync());
        Assert.Empty(await database.Service.GetCatalogProductsAsync());
        Assert.Null(await database.Service.GetProductByIdAsync(1));
    }

    /// <summary>
    /// Keeps SQLite alive while allowing the service to create independent contexts.
    /// </summary>
    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public TestContextFactory Factory { get; }
        public ProductService Service { get; }

        private TestDatabase(SqliteConnection connection)
        {
            this.connection = connection;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;
            Factory = new TestContextFactory(options);
            Service = new ProductService(Factory);
        }

        public static async Task<TestDatabase> CreateAsync(bool seedProducts = true)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var database = new TestDatabase(connection);

            await using var db = database.Factory.CreateDbContext();
            await db.Database.EnsureCreatedAsync();

            if (seedProducts)
            {
                // Animal types and categories are supplied by the context's model seed.
                db.Products.AddRange(
                    new Product
                    {
                        Name = "Alpha Food", AnimalTypeId = 1, CategoryId = 1,
                        Category = "Food", Price = 10m,
                        Variants =
                        [
                            new ProductVariant { Label = "2.2 lb", Price = 12m },
                            new ProductVariant { Label = "11 lb", Price = 30m }
                        ]
                    },
                    new Product
                    {
                        Name = "Beta Collar", AnimalTypeId = 2, CategoryId = 2,
                        Category = "Food", Price = 20m
                    },
                    new Product
                    {
                        Name = "Delta Unassigned", AnimalTypeId = 1,
                        CategoryId = null, Price = 25m
                    },
                    new Product
                    {
                        Name = "Gamma Food", AnimalTypeId = 2, CategoryId = 1,
                        Category = "Food", Price = 30m
                    });
                await db.SaveChangesAsync();
            }

            return database;
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }

    /// <summary>
    /// Creates a new context for each service operation using the isolated connection.
    /// </summary>
    private sealed class TestContextFactory(
        DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);

        public Task<ApplicationDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}