using PetShopApp.Components;
using Microsoft.EntityFrameworkCore;
using PetShopApp.Data;
using PetShopApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Added by Rebecca to make cart work
builder.Services.AddScoped<CartService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

//Rebecca's Database:

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

// Read the SQLite connection string from configuration.(sp)
// Stop startup with a clear error if the connection string is missing.(sp)
var connectionString = builder.Configuration.GetConnectionString("PetShopDatabase")
    ?? throw new InvalidOperationException("Missing PetShopDatabase connection string.");

//Register a factory so services can create a fresh SQlite context for each operation.(SP)
builder.Services.AddDbContextFactory<ApplicationDbContext>(
    options => options.UseSqlite(connectionString));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CartService>();

var app = builder.Build();

// Added by Rebecca: Helps seed the database for products.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    await ProductSeeder.SeedAsync(dbContext);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();