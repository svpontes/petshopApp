using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PetShopApp.Data;
using PetShopApp.Models;

namespace PetShopApp.Tests;

/// <summary>
/// Verifies the persisted order aggregate, owner deletion rules and safe migration paths.
/// Every test uses its own temporary SQLite file and never opens a developer's database.
/// </summary>
public sealed class OrderPersistenceTests
{
    private const string PreviousMigration = "20261128110724_AddProductStock";
    private const string CurrentMigration = "20261128125842_ConfigureOrderPersistence";

    /// <summary>The model explicitly maps snapshots and prevents account deletion from detaching history.</summary>
    [Fact]
    public void Model_MapsTheExistingAggregateAndNonCascadingOwnerRelationship()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:").Options);
        var order = db.Model.FindEntityType(typeof(Order))!;
        Assert.Null(db.Model.FindEntityType(typeof(OrderItem)));
        Assert.Null(order.FindProperty(nameof(Order.OrderItems)));
        Assert.False(order.FindProperty(nameof(Order.SerializedItems))!.IsNullable);
        Assert.False(order.FindProperty(nameof(Order.CustomerName))!.IsNullable);
        Assert.False(order.FindProperty(nameof(Order.CustomerEmail))!.IsNullable);
        Assert.False(order.FindProperty(nameof(Order.ShippingAddress))!.IsNullable);
        Assert.False(order.FindProperty(nameof(Order.Status))!.IsNullable);
        Assert.True(order.FindProperty(nameof(Order.UserId))!.IsNullable);
        Assert.Equal("TEXT", order.FindProperty(nameof(Order.TotalAmount))!.GetColumnType());
        var owner = Assert.Single(order.GetForeignKeys());
        Assert.Equal(typeof(User), owner.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.ClientNoAction, owner.DeleteBehavior);
        Assert.Equal(new[] { nameof(Order.UserId), nameof(Order.OrderDate), nameof(Order.Id) },
            Assert.Single(order.GetIndexes()).Properties.Select(property => property.Name));
    }

    /// <summary>One save persists the complete order and both independent purchase-time item values.</summary>
    [Fact]
    public async Task SaveAndRetrieve_PreservesOwnerContactDateOptionsQuantitiesAndDecimalPrices()
    {
        await using var setup = await OrderDatabase.CreateAsync();
        var id = await setup.SaveOrderAsync();
        await using var db = setup.NewContext();
        var order = await db.Orders.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(setup.BuyerId, order.UserId);
        Assert.Equal(OrderDatabase.PurchaseDate, order.OrderDate);
        Assert.Equal("Original Buyer", order.CustomerName);
        Assert.Equal("buyer@example.invalid", order.CustomerEmail);
        Assert.Equal("Original address", order.ShippingAddress);
        Assert.Equal(30.60m, order.TotalAmount);
        var items = order.OrderItems;
        Assert.Equal(2, items.Count);
        Assert.Equal("2.2 lb", items[0].Size);
        Assert.Equal(2, items[0].Quantity);
        Assert.Equal(10.80m, items[0].Price);
        Assert.Equal("/images/food.png", items[0].ImageUrl);
        Assert.Equal("", items[1].Size);
        Assert.Equal(9m, items[1].Price);
    }

    /// <summary>Live product, option and customer edits cannot change saved purchase values.</summary>
    [Fact]
    public async Task CatalogAndProfileChanges_DoNotRewriteHistoricalSnapshots()
    {
        await using var setup = await OrderDatabase.CreateAsync();
        var id = await setup.SaveOrderAsync();
        await using var db = setup.NewContext();
        var original = await db.Orders.AsNoTracking().SingleAsync(order => order.Id == id);
        var product = await db.Products.Include(item => item.Variants).SingleAsync(item => item.Id == setup.FoodId);
        product.Name = "Renamed current food";
        product.Price = 100m;
        product.ImageUrl = "/images/new-image.png";
        product.Variants[0].Label = "New label";
        product.Variants[0].Price = 200m;
        product.Variants[0].StockQuantity = 0;
        var buyer = (await db.Users.FindAsync(setup.BuyerId))!;
        buyer.Name = "Current profile name";
        buyer.Email = "changed@example.invalid";
        buyer.Address = "Current address";
        await db.SaveChangesAsync();
        var saved = await db.Orders.AsNoTracking().SingleAsync(order => order.Id == id);
        Assert.Equal(original.SerializedItems, saved.SerializedItems);
        Assert.Equal(30.60m, saved.TotalAmount);
        Assert.Equal("Original Buyer", saved.CustomerName);
        Assert.Equal("buyer@example.invalid", saved.CustomerEmail);
        Assert.Equal("Original address", saved.ShippingAddress);
    }

    /// <summary>Deleting a product, its variants and its now-unused category must leave the order intact.</summary>
    [Fact]
    public async Task CatalogDeletion_DoesNotDeleteOrDetachPurchaseSnapshots()
    {
        await using var setup = await OrderDatabase.CreateAsync();
        var id = await setup.SaveOrderAsync();
        await using var db = setup.NewContext();
        var original = await db.Orders.AsNoTracking().SingleAsync(order => order.Id == id);
        db.Products.Remove((await db.Products.FindAsync(setup.FoodId))!);
        await db.SaveChangesAsync();
        db.Categories.Remove((await db.Categories.FindAsync(1))!);
        await db.SaveChangesAsync();
        Assert.Empty(await db.ProductVariants.ToListAsync());
        var saved = await db.Orders.AsNoTracking().SingleAsync(order => order.Id == id);
        Assert.Equal(original.SerializedItems, saved.SerializedItems);
        Assert.Equal(setup.BuyerId, saved.UserId);
        Assert.Equal(setup.FoodId, saved.OrderItems[0].ProductId);
        Assert.Equal("Original food", saved.OrderItems[0].ProductName);
        Assert.Equal(30.60m, saved.TotalAmount);
    }

    /// <summary>Both tracked and untracked order histories must block deletion of their owner.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AccountDeletion_WithOrdersIsRejectedWithoutClearingOwnership(bool includeHistory)
    {
        await using var setup = await OrderDatabase.CreateAsync();
        var id = await setup.SaveOrderAsync();
        await using (var db = setup.NewContext())
        {
            var buyer = includeHistory
                ? await db.Users.Include(user => user.OrderHistory).SingleAsync(user => user.Id == setup.BuyerId)
                : (await db.Users.FindAsync(setup.BuyerId))!;
            db.Users.Remove(buyer);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using var verify = setup.NewContext();
        Assert.NotNull(await verify.Users.FindAsync(setup.BuyerId));
        Assert.Equal(setup.BuyerId, (await verify.Orders.FindAsync(id))!.UserId);
        Assert.Equal(2, (await verify.Orders.FindAsync(id))!.OrderItems.Count);
    }

    /// <summary>The noncascading rule must not prevent deleting an account with no orders.</summary>
    [Fact]
    public async Task AccountDeletion_WithoutOrdersIsAllowed()
    {
        await using var setup = await OrderDatabase.CreateAsync();
        await using var db = setup.NewContext();
        db.Users.Remove((await db.Users.FindAsync(setup.BuyerId))!);
        await db.SaveChangesAsync();
        Assert.Empty(await db.Users.ToListAsync());
    }

    /// <summary>Legacy ownerless rows remain readable; this does not enable guest checkout.</summary>
    [Fact]
    public async Task LegacyUnassociatedOrder_PreservesItsSnapshotAndCanBeRead()
    {
        await using var setup = await OrderDatabase.CreateAsync();
        await using var db = setup.NewContext();
        var order = setup.NewOrder();
        order.UserId = null;
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await using var verify = setup.NewContext();
        var saved = await verify.Orders.SingleAsync();
        Assert.Null(saved.UserId);
        Assert.Equal(2, saved.OrderItems.Count);
    }

    /// <summary>A fabricated owner ID must not create a saved order.</summary>
    [Fact]
    public async Task InvalidOwner_IsRejectedByTheDatabase()
    {
        await using var setup = await OrderDatabase.CreateAsync();
        await using (var db = setup.NewContext())
        {
            var order = setup.NewOrder();
            order.UserId = int.MaxValue;
            db.Orders.Add(order);
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using var verify = setup.NewContext();
        Assert.Empty(await verify.Orders.ToListAsync());
    }

    /// <summary>The configured required columns remain NOT NULL in SQLite.</summary>
    [Theory]
    [InlineData(nameof(Order.Status))]
    [InlineData(nameof(Order.CustomerName))]
    [InlineData(nameof(Order.CustomerEmail))]
    [InlineData(nameof(Order.ShippingAddress))]
    [InlineData(nameof(Order.SerializedItems))]
    public async Task RequiredColumns_RejectNullValuesWithoutSavingTheOrder(string property)
    {
        await using var setup = await OrderDatabase.CreateAsync();
        await using (var db = setup.NewContext())
        {
            var order = setup.NewOrder();
            db.Orders.Add(order);
            db.Entry(order).Property(property).CurrentValue = null;
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using var verify = setup.NewContext();
        Assert.Empty(await verify.Orders.ToListAsync());
    }

    /// <summary>Status-only updates must not normalize, drop unknown fields or reserialize raw historical JSON.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RawLegacySnapshots_ArePreservedDuringUnrelatedUpdates(bool unreadable)
    {
        await using var setup = await OrderDatabase.CreateAsync();
        var raw = unreadable ? "{unreadable legacy payload}" : "[\n {\"productid\":17,\"productname\":\"Legacy food\",\"price\":12.345,\"quantity\":2,\"size\":\"2.2 lb\",\"legacyMetadata\":\"keep me\"}\n]";
        await using (var db = setup.NewContext())
        {
            var order = setup.NewOrder();
            order.SerializedItems = raw;
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }
        await using (var db = setup.NewContext())
        {
            var order = await db.Orders.SingleAsync();
            if (unreadable) Assert.Empty(order.OrderItems);
            else
            {
                var item = Assert.Single(order.OrderItems);
                Assert.Equal("Legacy food", item.ProductName);
                Assert.Equal(12.345m, item.Price);
            }
            order.Status = "Shipped";
            await db.SaveChangesAsync();
        }
        await using var verify = setup.NewContext();
        Assert.Equal(raw, (await verify.Orders.SingleAsync()).SerializedItems);
    }

    /// <summary>All migrations must build a new database and permit saving a complete aggregate.</summary>
    [Fact]
    public async Task Migrations_CreateFreshDatabaseWithTheExpectedHistoryIndex()
    {
        await using var setup = new OrderDatabase();
        await using var db = setup.NewContext();
        await db.Database.MigrateAsync();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await setup.SeedAsync();
        var id = await setup.SaveOrderAsync();
        Assert.Equal(2, (await db.Orders.AsNoTracking().SingleAsync(order => order.Id == id)).OrderItems.Count);
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND name='IX_Orders_UserId_OrderDate_Id';";
        var indexSql = (string)(await command.ExecuteScalarAsync())!;
        Assert.Contains("\"OrderDate\" DESC", indexSql);
        Assert.Contains("\"Id\" DESC", indexSql);
    }

    /// <summary>Upgrade, rerun and rollback preserve every stored column of catalog, accounts and orders.</summary>
    [Fact]
    public async Task Migration_UpgradesExistingDataAndRollsBackWithoutRewritingAnyRows()
    {
        await using var setup = new OrderDatabase();
        await using var db = setup.NewContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await setup.SeedAsync();
        await setup.SaveOrderAsync();
        var legacy = setup.NewOrder();
        legacy.UserId = null;
        legacy.SerializedItems = "[ {\"productid\":123,\"quantity\":2,\"price\":3.125,\"size\":\"old label\",\"legacy\":true} ]";
        db.Orders.Add(legacy);
        await db.SaveChangesAsync();
        var before = await SnapshotDatabaseAsync(db);

        await migrator.MigrateAsync(CurrentMigration);
        Assert.Equal(before, await SnapshotDatabaseAsync(db));
        await migrator.MigrateAsync(CurrentMigration);
        Assert.Equal(before, await SnapshotDatabaseAsync(db));
        await migrator.MigrateAsync(PreviousMigration);
        Assert.Equal(before, await SnapshotDatabaseAsync(db));
        await migrator.MigrateAsync(CurrentMigration);
        Assert.Equal(before, await SnapshotDatabaseAsync(db));
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }

    /// <summary>Compare raw column values so JSON whitespace, timestamps and credential fields are covered.</summary>
    private static async Task<string> SnapshotDatabaseAsync(ApplicationDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        var result = new SortedDictionary<string, List<SortedDictionary<string, string?>>>();
        foreach (var table in new[] { "Categories", "Products", "ProductVariants", "Users", "Orders" })
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            // Table names are fixed test constants, never user input.
            command.CommandText = $"SELECT * FROM \"{table}\" ORDER BY \"Id\";";
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<SortedDictionary<string, string?>>();
            while (await reader.ReadAsync())
            {
                var row = new SortedDictionary<string, string?>();
                for (var column = 0; column < reader.FieldCount; column++)
                    row[reader.GetName(column)] = reader.IsDBNull(column) ? null
                        : Convert.ToString(reader.GetValue(column), CultureInfo.InvariantCulture);
                rows.Add(row);
            }
            result[table] = rows;
        }
        return JsonSerializer.Serialize(result);
    }

    /// <summary>Contains deterministic, nonproduction data in a disposable database.</summary>
    private sealed class OrderDatabase : IAsyncDisposable
    {
        public static readonly DateTime PurchaseDate = new(2026, 11, 20, 15, 30, 0, DateTimeKind.Utc);
        private readonly string path = Path.Combine(Path.GetTempPath(), $"petshop-order-persistence-{Guid.NewGuid():N}.db");
        private readonly DbContextOptions<ApplicationDbContext> options;
        public int BuyerId { get; private set; }
        public int FoodId { get; private set; }
        private int collarId;

        public OrderDatabase()
        {
            var connection = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString();
            options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        }
        public ApplicationDbContext NewContext() => new(options);

        public static async Task<OrderDatabase> CreateAsync()
        {
            var setup = new OrderDatabase();
            await using var db = setup.NewContext();
            await db.Database.EnsureCreatedAsync();
            await setup.SeedAsync();
            return setup;
        }

        public async Task SeedAsync()
        {
            await using var db = NewContext();
            var buyer = new User
            {
                Name = "Original Buyer", Email = "buyer@example.invalid", UserName = "buyer@example.invalid",
                NormalizedEmail = "BUYER@EXAMPLE.INVALID", NormalizedUserName = "BUYER@EXAMPLE.INVALID",
                // This deliberately unusable value exists only in a disposable persistence test.
                PasswordHash = "not-a-login-credential", SecurityStamp = "persistence-test-stamp"
            };
            var food = new Product
            {
                Name = "Original food", Price = 12m, AnimalTypeId = 1, CategoryId = 1, Category = "Food",
                ImageUrl = "/images/food.png", SecondaryImageUrlsRaw = "/images/food-secondary.png",
                Variants = [new ProductVariant { Label = "2.2 lb", Price = 12m, StockQuantity = 4 }]
            };
            var collar = new Product { Name = "Collar", Price = 10m, AnimalTypeId = 2, CategoryId = 2, Category = "Accessories", StockQuantity = 3 };
            db.Users.Add(buyer);
            db.Products.AddRange(food, collar);
            await db.SaveChangesAsync();
            BuyerId = buyer.Id;
            FoodId = food.Id;
            collarId = collar.Id;
        }

        public Order NewOrder() => new()
        {
            UserId = BuyerId, OrderDate = PurchaseDate, CustomerName = "Original Buyer",
            CustomerEmail = "buyer@example.invalid", ShippingAddress = "Original address", TotalAmount = 30.60m,
            OrderItems = [
                new OrderItem { ProductId = FoodId, ProductName = "Original food", ImageUrl = "/images/food.png", Size = "2.2 lb", Quantity = 2, Price = 10.80m },
                new OrderItem { ProductId = collarId, ProductName = "Collar", Quantity = 1, Price = 9m }
            ]
        };

        public async Task<int> SaveOrderAsync()
        {
            await using var db = NewContext();
            var order = NewOrder();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
            return order.Id;
        }

        public ValueTask DisposeAsync()
        {
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                if (File.Exists(file)) File.Delete(file);
            return ValueTask.CompletedTask;
        }
    }
}
