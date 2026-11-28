using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PetShopApp.Components.Shared;
using PetShopApp.Data;
using PetShopApp.Models;
using PetShopApp.Services;

namespace PetShopApp.Tests;

/// <summary>
/// Covers stock pools, rollback, rendering, migration and concurrent checkouts with real SQLite.
/// Each database belongs to this test and is deleted afterwards; no developer account is used.
/// </summary>
public sealed class StockTests
{
    /// <summary>A cart must limit combined selections without changing physical stock.</summary>
    [Fact]
    public void Cart_RejectsCombinedQuantitiesAndAllowsRemovalWithoutChangingInventory()
    {
        var product = new Product { Id = 1, Name = "Collar", Price = 10m, StockQuantity = 3 };
        var cart = new CartService();
        Assert.True(cart.AddToCart(product, "", 2));
        Assert.False(cart.AddToCart(product, "", 2));
        Assert.Equal(2, Assert.Single(cart.CartItems).Quantity);
        Assert.Equal(3, product.StockQuantity);
        Assert.True(cart.UpdateQuantity(1, "", 1));
        Assert.False(cart.UpdateQuantity(1, "", 1));
        cart.RemoveFromCart(1, "");
        Assert.Equal(3, cart.RemainingQuantity(product));
        Assert.Equal(3, product.StockQuantity);
    }

    /// <summary>Different CSV labels must consume the same cart allowance.</summary>
    [Fact]
    public void Cart_CsvSizesShareStockWhilePricedVariantsHaveIndependentPools()
    {
        var cart = new CartService();
        var product = new Product { Id = 1, Price = 10m, StockQuantity = 3, AvailableOptionsRaw = "S, M" };
        Assert.True(cart.AddToCart(product, "S", 2));
        Assert.False(cart.AddToCart(product, "M", 2));
        Assert.True(cart.AddToCart(product, "M", 1));
        Assert.Equal(0, cart.RemainingQuantity(product, "S"));

        var food = new Product { Id = 2, Price = 100m, StockQuantity = 0 };
        var small = new ProductVariant { Id = 1, ProductId = 2, Label = "2.2 lb", Price = 10m, StockQuantity = 1 };
        var large = new ProductVariant { Id = 2, ProductId = 2, Label = "11 lb", Price = 30m, StockQuantity = 2 };
        food.Variants.AddRange([small, large]);
        Assert.True(cart.AddToCart(food, small.Label, 1, small));
        Assert.True(cart.AddToCart(food, large.Label, 2, large));
        Assert.False(cart.AddToCart(food, small.Label, 1, small));
    }

    /// <summary>A supplied variant object cannot invent a lower price or higher stock.</summary>
    [Fact]
    public void Cart_UsesTheOwnedVariantAndRejectsSoldOutOrInvalidSelections()
    {
        var product = new Product { Id = 1, StockQuantity = 100 };
        var actual = new ProductVariant { Id = 3, ProductId = 1, Label = "11 lb", Price = 30m, StockQuantity = 1 };
        product.Variants.Add(actual);
        var forged = new ProductVariant { Id = 3, ProductId = 1, Label = "11 lb", Price = 1m, StockQuantity = 100 };
        var cart = new CartService();
        Assert.False(cart.AddToCart(product, "", 1));
        Assert.False(cart.AddToCart(product, "11 lb", 2, forged));
        Assert.True(cart.AddToCart(product, "11 lb", 1, forged));
        Assert.Equal(27m, Assert.Single(cart.CartItems).UnitPrice);
        Assert.False(ProductStock.IsSoldOut(product));
        actual.StockQuantity = 0;
        Assert.True(ProductStock.IsSoldOut(product));
        Assert.False(cart.AddToCart(product, "11 lb", 1, actual));
    }

    /// <summary>Products added by old code default to unavailable until an administrator counts them.</summary>
    [Fact]
    public void Cart_RejectsDefaultZeroStock()
    {
        var product = new Product { Id = 1, Price = 10m };
        var cart = new CartService();
        Assert.True(ProductStock.IsSoldOut(product));
        Assert.False(cart.AddToCart(product, "", 1));
        Assert.Empty(cart.CartItems);
        Assert.Contains("OUT OF STOCK", cart.LastError);
    }

    /// <summary>Checkout decrements only purchased pools and preserves prices and quantities in history.</summary>
    [Fact]
    public async Task Checkout_SavesTheOrderAndDecrementsOnlyPurchasedStock()
    {
        await using var setup = await StockDatabase.CreateAsync();
        await using var scope = await setup.CustomerScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<OrderService>();
        var id = await service.CreateOrderAsync(Request(
            new() { ProductId = setup.CollarId, Quantity = 2 },
            new() { ProductId = setup.FoodId, Size = "2.2 lb", Quantity = 1 }));
        await using var db = setup.NewContext();
        Assert.Equal(1, (await db.Products.FindAsync(setup.CollarId))!.StockQuantity);
        var variants = await db.ProductVariants.OrderBy(variant => variant.Id).ToListAsync();
        Assert.Equal(1, variants[0].StockQuantity);
        Assert.Equal(4, variants[1].StockQuantity);
        var order = await db.Orders.FindAsync(id);
        Assert.NotNull(order);
        Assert.Equal(28.80m, order.TotalAmount);
        Assert.Equal(2, order.OrderItems.Count);
        Assert.Equal(2, order.OrderItems[0].Quantity);
        Assert.Equal(10.80m, order.OrderItems[1].Price);
    }

    /// <summary>Repeated variant request lines must not evade the combined stock check.</summary>
    [Fact]
    public async Task Checkout_RejectsRepeatedLinesBeyondVariantStock()
    {
        await using var setup = await StockDatabase.CreateAsync();
        await using var scope = await setup.CustomerScopeAsync();
        var service = scope.ServiceProvider.GetRequiredService<OrderService>();
        await Assert.ThrowsAsync<StockUnavailableException>(() => service.CreateOrderAsync(Request(
            new() { ProductId = setup.FoodId, Size = "2.2 lb", Quantity = 2 },
            new() { ProductId = setup.FoodId, Size = "2.2 lb", Quantity = 1 })));
        await using var db = setup.NewContext();
        Assert.Empty(await db.Orders.ToListAsync());
        Assert.Equal(2, (await db.ProductVariants.FirstAsync(variant => variant.Label == "2.2 lb")).StockQuantity);
    }

    /// <summary>Different CSV selections share stock at checkout as well as in the cart.</summary>
    [Fact]
    public async Task Checkout_RejectsCombinedCsvLinesBeyondSharedStock()
    {
        await using var setup = await StockDatabase.CreateAsync();
        await using var db = setup.NewContext();
        await db.Products.Where(product => product.Id == setup.CollarId)
            .ExecuteUpdateAsync(update => update.SetProperty(product => product.AvailableOptionsRaw, "S, M"));
        await using var scope = await setup.CustomerScopeAsync();
        await Assert.ThrowsAsync<StockUnavailableException>(() => scope.ServiceProvider.GetRequiredService<OrderService>()
            .CreateOrderAsync(Request(
                new() { ProductId = setup.CollarId, Size = "S", Quantity = 2 },
                new() { ProductId = setup.CollarId, Size = "M", Quantity = 2 })));
        Assert.Empty(await db.Orders.ToListAsync());
        Assert.Equal(3, (await db.Products.AsNoTracking().SingleAsync(product => product.Id == setup.CollarId)).StockQuantity);
    }

    /// <summary>A later unavailable line rolls back an earlier successful decrement.</summary>
    [Fact]
    public async Task Checkout_RollsBackAllLinesWhenALaterPoolIsUnavailable()
    {
        await using var setup = await StockDatabase.CreateAsync();
        await using var scope = await setup.CustomerScopeAsync();
        await Assert.ThrowsAsync<StockUnavailableException>(() => scope.ServiceProvider.GetRequiredService<OrderService>()
            .CreateOrderAsync(Request(
                new() { ProductId = setup.CollarId, Quantity = 1 },
                new() { ProductId = setup.FoodId, Size = "2.2 lb", Quantity = 3 })));
        await using var db = setup.NewContext();
        Assert.Equal(3, (await db.Products.FindAsync(setup.CollarId))!.StockQuantity);
        Assert.Empty(await db.Orders.ToListAsync());
    }

    /// <summary>A failed order INSERT must not leave inventory deducted.</summary>
    [Fact]
    public async Task Checkout_RollsBackStockWhenOrderSavingFails()
    {
        await using var setup = await StockDatabase.CreateAsync();
        await using var db = setup.NewContext();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectOrder BEFORE INSERT ON Orders BEGIN SELECT RAISE(ABORT, 'test failure'); END;");
        await using var scope = await setup.CustomerScopeAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => scope.ServiceProvider.GetRequiredService<OrderService>()
            .CreateOrderAsync(Request(new CreateOrderLine() { ProductId = setup.CollarId, Quantity = 1 })));
        Assert.Equal(3, (await db.Products.FindAsync(setup.CollarId))!.StockQuantity);
        Assert.Empty(await db.Orders.ToListAsync());
    }

    /// <summary>Two independent customers cannot both purchase the last unit.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentCheckout_AllowsOnlyOnePurchaseOfTheLastUnit(bool variant)
    {
        await using var setup = await StockDatabase.CreateAsync();
        await using (var db = setup.NewContext())
        {
            if (variant)
                await db.ProductVariants.Where(item => item.Label == "2.2 lb")
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.StockQuantity, 1));
            else
                await db.Products.Where(item => item.Id == setup.CollarId)
                    .ExecuteUpdateAsync(update => update.SetProperty(item => item.StockQuantity, 1));
        }
        using var start = new Barrier(2);
        async Task<bool> BuyAsync(bool secondCustomer)
        {
            await using var scope = await setup.CustomerScopeAsync(secondCustomer);
            start.SignalAndWait(TimeSpan.FromSeconds(20));
            try
            {
                await scope.ServiceProvider.GetRequiredService<OrderService>().CreateOrderAsync(Request(new CreateOrderLine()
                {
                    ProductId = variant ? setup.FoodId : setup.CollarId,
                    Size = variant ? "2.2 lb" : "", Quantity = 1
                }));
                return true;
            }
            catch (StockUnavailableException) { return false; }
        }
        var outcomes = await Task.WhenAll(Task.Run(() => BuyAsync(false)), Task.Run(() => BuyAsync(true)));
        Assert.Single(outcomes, success => success);
        await using var verify = setup.NewContext();
        Assert.Single(await verify.Orders.ToListAsync());
        Assert.Equal(0, variant
            ? (await verify.ProductVariants.SingleAsync(item => item.Label == "2.2 lb")).StockQuantity
            : (await verify.Products.FindAsync(setup.CollarId))!.StockQuantity);
    }

    /// <summary>Database constraints reject negative direct writes to either inventory table.</summary>
    [Fact]
    public async Task Database_RejectsNegativeProductAndVariantStock()
    {
        await using var setup = await StockDatabase.CreateAsync();
        await using var db = setup.NewContext();
        await Assert.ThrowsAsync<SqliteException>(() => db.Products.Where(product => product.Id == setup.CollarId)
            .ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity, -1)));
        await Assert.ThrowsAsync<SqliteException>(() => db.ProductVariants.Where(variant => variant.Label == "2.2 lb")
            .ExecuteUpdateAsync(update => update.SetProperty(variant => variant.StockQuantity, -1)));
    }

    /// <summary>Migration must preserve existing catalog and historical order data.</summary>
    [Fact]
    public async Task Migration_PreservesExistingRowsAndStartsInventoryAtZero()
    {
        await using var setup = new StockDatabase();
        await using var db = setup.NewContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261126114336_AddIdentityInfrastructure");
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO Products (Name, Description, MoreInformation, AnimalTypeId, Category, CategoryId, Price, ImageUrl, SecondaryImageUrlsRaw, AvailableOptionsRaw)
            VALUES ('Existing food', '', '', 1, 'Food', 1, '12.0', '/images/test.png', '', '');
            INSERT INTO ProductVariants (ProductId, Label, Price) VALUES (1, '2.2 lb', '12.0');
            INSERT INTO Orders (OrderDate, TotalAmount, Status, CustomerName, CustomerEmail, ShippingAddress, SerializedItems)
            VALUES ('2026-11-28', '10.8', 'Processing', 'Existing buyer', 'buyer@example.invalid', 'Test address', '[]');
            """);
        await migrator.MigrateAsync();
        Assert.Equal(0, (await db.Products.SingleAsync()).StockQuantity);
        Assert.Equal(0, (await db.ProductVariants.SingleAsync()).StockQuantity);
        Assert.Equal("Existing food", (await db.Products.SingleAsync()).Name);
        Assert.Equal("[]", (await db.Orders.SingleAsync()).SerializedItems);
        // A rerun must be harmless rather than recreating or resetting rows.
        await db.Database.MigrateAsync();
        Assert.Single(await db.Orders.ToListAsync());
    }

    /// <summary>Server rendering must expose stock status without a false Add to Bag success.</summary>
    [Fact]
    public async Task QuickAdd_RenderingShowsSoldOutOrAvailableInventory()
    {
        await using var setup = await StockDatabase.CreateAsync();
        await using var scope = setup.Provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        async Task<string> RenderAsync() => await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<ProductQuickView>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { ["Show"] = true, ["Product"] = new Product { Id = setup.CollarId } }));
            return component.ToHtmlString();
        });
        var available = await RenderAsync();
        Assert.Contains("3 unit(s) in stock", available);
        await using var db = setup.NewContext();
        await db.Products.Where(product => product.Id == setup.CollarId)
            .ExecuteUpdateAsync(update => update.SetProperty(product => product.StockQuantity, 0));
        var soldOut = await RenderAsync();
        Assert.Contains("OUT OF STOCK", soldOut);
    }

    /// <summary>A sold-out first variant must not hide another available option.</summary>
    [Fact]
    public async Task QuickAdd_SelectsAnAvailableVariantWhenTheFirstWeightIsSoldOut()
    {
        await using var setup = await StockDatabase.CreateAsync();
        await using var db = setup.NewContext();
        await db.ProductVariants.Where(variant => variant.Label == "2.2 lb")
            .ExecuteUpdateAsync(update => update.SetProperty(variant => variant.StockQuantity, 0));
        await using var scope = setup.Provider.CreateAsyncScope();
        await using var renderer = new HtmlRenderer(scope.ServiceProvider,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<ProductQuickView>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { ["Show"] = true, ["Product"] = new Product { Id = setup.FoodId } }));
            return component.ToHtmlString();
        });
        Assert.Contains("OUT OF STOCK", html);
        Assert.Contains("4 unit(s) in stock", html);
        Assert.Contains("aria-pressed=\"true\"", html);
        var current = await db.Products.Include(item => item.Variants).SingleAsync(item => item.Id == setup.FoodId);
        Assert.False(ProductStock.IsSoldOut(current));
    }

    // Requests use the same contact fields validated by the real checkout service.
    private static CreateOrderRequest Request(params CreateOrderLine[] lines) => new()
    {
        CustomerName = "Stock Test", CustomerEmail = "stock@example.invalid",
        CustomerPhone = "123456789", ShippingAddress = "Test street 1", Items = lines.ToList()
    };

    /// <summary>File-backed SQLite provides genuinely separate connections for concurrent requests.</summary>
    private sealed class StockDatabase : IAsyncDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"petshop-stock-{Guid.NewGuid():N}.db");
        public ServiceProvider Provider { get; }
        public int CollarId { get; private set; }
        public int FoodId { get; private set; }
        private int customerId;
        private int secondCustomerId;
        private readonly DbContextOptions<ApplicationDbContext> options;

        public StockDatabase()
        {
            var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = false, DefaultTimeout = 15 }.ToString();
            options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connectionString).Options;
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContextFactory<ApplicationDbContext>(builder => builder.UseSqlite(connectionString));
            services.AddIdentityCore<User>().AddRoles<IdentityRole<int>>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddScoped<AuthService>();
            services.AddScoped<OrderService>();
            services.AddScoped<ProductService>();
            services.AddScoped<CartService>();
            Provider = services.BuildServiceProvider();
        }
        public ApplicationDbContext NewContext() => new(options);

        public static async Task<StockDatabase> CreateAsync()
        {
            var setup = new StockDatabase();
            await using var scope = setup.Provider.CreateAsyncScope();
            await using var db = setup.NewContext();
            await db.Database.EnsureCreatedAsync();
            await IdentityDataInitializer.InitializeAsync(scope.ServiceProvider);
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var customer = new User { Name = "Stock test", Email = "stock@example.invalid", UserName = "stock@example.invalid" };
            var created = await users.CreateAsync(customer, "Stock#2026Test");
            Assert.True(created.Succeeded);
            setup.customerId = customer.Id;
            var otherCustomer = new User { Name = "Second buyer", Email = "other@example.invalid", UserName = "other@example.invalid" };
            Assert.True((await users.CreateAsync(otherCustomer, "Stock#2026Test")).Succeeded);
            setup.secondCustomerId = otherCustomer.Id;
            var collar = new Product { Name = "Collar", AnimalTypeId = 2, Price = 10m, StockQuantity = 3 };
            var food = new Product { Name = "Food", AnimalTypeId = 1, Price = 12m,
                Variants = [new() { Label = "2.2 lb", Price = 12m, StockQuantity = 2 }, new() { Label = "11 lb", Price = 30m, StockQuantity = 4 }] };
            db.Products.AddRange(collar, food);
            await db.SaveChangesAsync();
            setup.CollarId = collar.Id;
            setup.FoodId = food.Id;
            return setup;
        }

        public async Task<AsyncServiceScope> CustomerScopeAsync(bool secondCustomer = false)
        {
            var scope = Provider.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var customer = (await users.FindByIdAsync((secondCustomer ? secondCustomerId : customerId).ToString()))!;
            var claims = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<User>>();
            await scope.ServiceProvider.GetRequiredService<AuthService>().InitializeAsync(await claims.CreateAsync(customer));
            return scope;
        }

        public async ValueTask DisposeAsync()
        {
            await Provider.DisposeAsync();
            foreach (var file in new[] { path, path + "-wal", path + "-shm" })
                if (File.Exists(file)) File.Delete(file);
        }
    }
}
