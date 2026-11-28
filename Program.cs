using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PetShopApp.Components;
using PetShopApp.Data;
using PetShopApp.Models;
using PetShopApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Register interactive Razor components.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Use the configured SQLite database and create contexts per operation.
var connectionString = builder.Configuration.GetConnectionString("PetShopDatabase")
    ?? throw new InvalidOperationException("Missing PetShopDatabase connection string.");

builder.Services.AddDbContextFactory<ApplicationDbContext>(
    options => options.UseSqlite(connectionString));

// Register Identity cookie schemes before configuring the account managers.
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
}).AddIdentityCookies();

builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
// Revalidate connected circuits and keep cart selections across HTTP sign-in.
builder.Services.AddScoped<AuthenticationStateProvider,
    IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddScoped<ProtectedSessionStorage>();
builder.Services.AddScoped<CartSessionStore>();
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
    options.ValidationInterval = TimeSpan.FromMinutes(1));

// Match the password rules already displayed by the registration page.
builder.Services.AddIdentityCore<User>(options =>
{
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters = string.Empty;
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

    // Confirmation messages are a separate feature; do not require unimplemented delivery.
    options.SignIn.RequireConfirmedAccount = false;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddRoles<IdentityRole<int>>()
.AddPasswordValidator<RegistrationPasswordValidator>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// Keep authentication cookies inaccessible to browser scripts.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "PetShopApp.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/";
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = true;
    options.Events.OnValidatePrincipal = async context =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(context);
        if (context.Principal?.Identity?.IsAuthenticated == true &&
            !await IdentityRevalidatingAuthenticationStateProvider.ValidatePrincipalAsync(
                context.HttpContext.RequestServices, context.Principal))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        }
    };
});

// Retain the existing application services during the authentication transition.
builder.Services.AddScoped<ProductService>();
builder.Services.AddScoped<ProductService>();
// Register authorized product administration operations.
builder.Services.AddScoped<ProductAdministrationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<OrderService>();
// Register order creation and customer order history services.
builder.Services.AddScoped<CustomerOrderHistoryService>();

// Build the application after registering all required services.
var app = builder.Build();
// Run local role setup only when explicitly requested from the command line.
if (args.Length > 0 && args[0] == "--grant-development-admin")
{
    if (args.Length != 2)
        throw new InvalidOperationException(
            "Usage: --grant-development-admin <registered-local-email>");

    await DevelopmentAdminSetup.GrantAsync(app.Services, app.Environment, args[1]);
    return;
}
// Apply migrations and prepare catalog data before initializing Identity.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider
        .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();

    await using var dbContext = await factory.CreateDbContextAsync();

    await ProductSeeder.SeedAsync(dbContext);
    await IdentityDataInitializer.InitializeAsync(scope.ServiceProvider);
}

// Configure production exception handling and HTTPS protection.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Configure error pages and HTTPS redirection.
app.UseStatusCodePagesWithReExecute(
    "/not-found",
    createScopeForStatusCodePages: true);

app.UseHttpsRedirection();

// Restore authentication before authorization and antiforgery validation.
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Register HTTP endpoints that create and remove authentication cookies.
app.MapAccountEndpoints();

// Map static files and interactive Razor components.
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Start the server and keep the application running.
app.Run();