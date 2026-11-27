using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PetShopApp.Data;
using PetShopApp.Models;
using PetShopApp.Services;

namespace PetShopApp.Tests;

/// <summary>
/// Verifies authorization against real Identity memberships in isolated SQLite databases.
/// No test reads or modifies a developer's database or creates a production account.
/// </summary>
public sealed class IdentityAuthorizationTests
{
    [Fact]
    public async Task AnonymousAndCustomerAccounts_CannotAuthorizeAdminOperations()
    {
        await using var setup = await TestAccounts.CreateAsync();
        Assert.False(await setup.Auth.IsAuthorizedAsync());
        Assert.False(await setup.Auth.IsAuthorizedAsync(requireAdmin: true));

        var user = await setup.RegisterAsync();
        await setup.SignInAsync(user);
        Assert.True(await setup.Auth.IsAuthorizedAsync());
        Assert.False(await setup.Auth.IsAuthorizedAsync(requireAdmin: true));
    }

    [Fact]
    public async Task ProtectedServices_RejectDirectAnonymousCalls()
    {
        await using var setup = await TestAccounts.CreateAsync();
        var factory = setup.Provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var history = new CustomerOrderHistoryService(factory, setup.Auth);
        var orders = new OrderService(factory, setup.Auth);

        // Calling the service directly must not bypass the page's authorization attribute.
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => history.GetMyOrdersAsync());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            orders.CreateOrderAsync(new CreateOrderRequest()));
    }

    [Fact]
    public async Task AdminClaimWithoutDatabaseMembership_DoesNotGrantAccess()
    {
        await using var setup = await TestAccounts.CreateAsync();
        var user = await setup.RegisterAsync();
        var principal = await setup.Claims.CreateAsync(user);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(ClaimTypes.Role, "Admin"));
        await setup.Auth.InitializeAsync(principal);
        Assert.False(await setup.Auth.IsAuthorizedAsync(requireAdmin: true));
    }

    [Fact]
    public async Task RemovingAdminMembership_RejectsAnAlreadyOpenAdminSession()
    {
        await using var setup = await TestAccounts.CreateAsync();
        var user = await setup.RegisterAsync();
        TestAccounts.Ensure(await setup.Users.AddToRoleAsync(user, "Admin"));
        await setup.SignInAsync(user);
        Assert.True(await setup.Auth.IsAuthorizedAsync(requireAdmin: true));

        // The UI has not reloaded; the next operation must still observe role removal.
        TestAccounts.Ensure(await setup.Users.RemoveFromRoleAsync(user, "Admin"));
        Assert.False(await setup.Auth.IsAuthorizedAsync(requireAdmin: true));
    }

    [Fact]
    public async Task ChangedSecurityStamp_RejectsTheOldSession()
    {
        await using var setup = await TestAccounts.CreateAsync();
        var user = await setup.RegisterAsync();
        await setup.SignInAsync(user);
        TestAccounts.Ensure(await setup.Users.UpdateSecurityStampAsync(user));
        Assert.False(await setup.Auth.IsAuthorizedAsync());
    }

    [Fact]
    public async Task DeletedAccount_CannotContinueUsingAnOpenSession()
    {
        await using var setup = await TestAccounts.CreateAsync();
        var user = await setup.RegisterAsync();
        await setup.SignInAsync(user);
        TestAccounts.Ensure(await setup.Users.DeleteAsync(user));
        Assert.False(await setup.Auth.IsAuthorizedAsync());
    }

    [Fact]
    public async Task DevelopmentSetup_PreservesCredentialsAndRequiresNewClaims()
    {
        await using var setup = await TestAccounts.CreateAsync();
        var user = await setup.RegisterAsync();
        var originalId = user.Id;
        var originalHash = user.PasswordHash;
        await setup.SignInAsync(user);

        await DevelopmentAdminSetup.GrantAsync(
            setup.Provider, new TestEnvironment("Development"), user.Email);
        Assert.False(await setup.Auth.IsAuthorizedAsync());

        await using var freshScope = setup.Provider.CreateAsyncScope();
        var users = freshScope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var updated = await users.FindByEmailAsync(user.Email);
        Assert.NotNull(updated);
        Assert.Equal(originalId, updated.Id);
        Assert.Equal(originalHash, updated.PasswordHash);
        Assert.True(await users.IsInRoleAsync(updated, "Admin"));
        Assert.True(await users.IsInRoleAsync(updated, "Customer"));

        // Repeating explicit setup must not change the account or security stamp.
        var stamp = updated.SecurityStamp;
        await DevelopmentAdminSetup.GrantAsync(
            setup.Provider, new TestEnvironment("Development"), user.Email);
        var factory = setup.Provider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        Assert.Equal(stamp, (await db.Users.AsNoTracking().SingleAsync()).SecurityStamp);

        await setup.SignInAsync(updated);
        Assert.True(await setup.Auth.IsAuthorizedAsync(requireAdmin: true));
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task DevelopmentSetup_RejectsOtherEnvironments(string environment)
    {
        await using var setup = await TestAccounts.CreateAsync();
        var user = await setup.RegisterAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentAdminSetup.GrantAsync(
                setup.Provider, new TestEnvironment(environment), user.Email));
        Assert.False(await setup.Users.IsInRoleAsync(user, "Admin"));
    }

    [Fact]
    public async Task DevelopmentSetup_RequiresAnExistingRegisteredAccount()
    {
        await using var setup = await TestAccounts.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DevelopmentAdminSetup.GrantAsync(
                setup.Provider, new TestEnvironment("Development"), "missing@example.com"));
        Assert.Empty(await setup.Users.Users.ToListAsync());
    }

    /// <summary>
    /// Keeps a separate in-memory SQLite connection alive for each individual test.
    /// </summary>
    private sealed class TestAccounts : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly AsyncServiceScope scope;
        public ServiceProvider Provider { get; }
        public UserManager<User> Users => scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        public IUserClaimsPrincipalFactory<User> Claims => scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<User>>();
        public AuthService Auth => scope.ServiceProvider.GetRequiredService<AuthService>();

        private TestAccounts(SqliteConnection connection, ServiceProvider provider)
        {
            this.connection = connection;
            Provider = provider;
            scope = provider.CreateAsyncScope();
        }

        public static async Task<TestAccounts> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContextFactory<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<User>().AddRoles<IdentityRole<int>>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddScoped<AuthService>();
            var setup = new TestAccounts(connection, services.BuildServiceProvider());
            var db = setup.scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            await IdentityDataInitializer.InitializeAsync(setup.scope.ServiceProvider);
            return setup;
        }

        public async Task<User> RegisterAsync()
        {
            // This password belongs only to an ephemeral test account.
            var user = new User { Name = "Identity test", UserName = "local@example.com", Email = "local@example.com" };
            Ensure(await Users.CreateAsync(user, "Tests#2026Only"));
            Ensure(await Users.AddToRoleAsync(user, "Customer"));
            return user;
        }

        public async Task SignInAsync(User user) =>
            await Auth.InitializeAsync(await Claims.CreateAsync(user));

        public static void Ensure(IdentityResult result) =>
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Code)));

        public async ValueTask DisposeAsync()
        {
            await scope.DisposeAsync();
            await Provider.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    /// <summary>
    /// Supplies an explicit environment without starting a web server.
    /// </summary>
    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "PetShopApp.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}