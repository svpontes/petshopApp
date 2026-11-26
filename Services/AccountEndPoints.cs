using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PetShopApp.Data;
using PetShopApp.Models;

namespace PetShopApp.Services;

/// <summary>
/// Handles Identity operations in HTTP requests so cookies reach the browser.
/// </summary>
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        // Each POST validates its antiforgery token before processing credentials.
        app.MapPost("/account/login", LoginAsync).AllowAnonymous();
        app.MapPost("/account/register", RegisterAsync).AllowAnonymous();
        app.MapPost("/account/logout", LogoutAsync).AllowAnonymous();
    }

    private static async Task<IResult> LoginAsync(
        HttpContext context, IAntiforgery antiforgery,
        UserManager<User> users, SignInManager<User> signIn,
        ILoggerFactory loggerFactory)
    {
        if (!await ValidateFormAsync(context, antiforgery))
            return Results.BadRequest("Invalid form submission. Reload the page and try again.");

        var form = await context.Request.ReadFormAsync();
        var email = form["email"].ToString().Trim().ToLowerInvariant();
        var password = form["password"].ToString();
        if (!IsValidEmail(email) || string.IsNullOrWhiteSpace(password))
            return Results.LocalRedirect("/login?error=credentials");

        var user = await users.FindByEmailAsync(email);
        if (user is null)
            return Results.LocalRedirect("/login?error=credentials");

        try
        {
            // Identity validates the hash and limits repeated failed login attempts.
            var result = await signIn.PasswordSignInAsync(
                user, password, isPersistent: false, lockoutOnFailure: true);
            if (!result.Succeeded)
                return Results.LocalRedirect("/login?error=credentials");
        }
        catch (FormatException)
        {
            loggerFactory.CreateLogger("AccountEndpoints")
                .LogWarning("An account has an unsupported password hash.");
            return Results.LocalRedirect("/login?error=credentials");
        }

        return Results.LocalRedirect(await users.IsInRoleAsync(user, "Admin") ? "/admin" : "/");
    }

    private static async Task<IResult> RegisterAsync(
        HttpContext context, IAntiforgery antiforgery,
        UserManager<User> users, SignInManager<User> signIn,
        ApplicationDbContext db, ILoggerFactory loggerFactory)
    {
        if (!await ValidateFormAsync(context, antiforgery))
            return Results.BadRequest("Invalid form submission. Reload the page and try again.");

        var form = await context.Request.ReadFormAsync();
        var name = form["name"].ToString().Trim();
        var email = form["email"].ToString().Trim().ToLowerInvariant();
        var password = form["password"].ToString();

        if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
            return Results.LocalRedirect("/register?error=name");
        if (!IsValidEmail(email))
            return Results.LocalRedirect("/register?error=email");
        if (await users.FindByEmailAsync(email) is not null)
            return Results.LocalRedirect("/register?error=duplicate");

        // Ignore role fields supplied by the browser; public accounts are customers.
        var user = new User
        {
            Name = name,
            UserName = email,
            Email = email,
            Role = "Customer",
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            // Create the account and its role together, or roll back both changes.
            await using var transaction = await db.Database.BeginTransactionAsync();
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var duplicate = result.Errors.Any(error =>
                    error.Code is "DuplicateEmail" or "DuplicateUserName");
                return Results.LocalRedirect(duplicate
                    ? "/register?error=duplicate"
                    : "/register?error=password");
            }

            var roleResult = await users.AddToRoleAsync(user, "Customer");
            if (!roleResult.Succeeded)
                throw new InvalidOperationException("Could not assign the Customer role.");
            await transaction.CommitAsync();
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            return Results.LocalRedirect("/register?error=duplicate");
        }
        catch (Exception exception)
        {
            loggerFactory.CreateLogger("AccountEndpoints")
                .LogError(exception, "Identity registration failed.");
            return Results.LocalRedirect("/register?error=unavailable");
        }

        // Complete sign-in only after the account and membership have been committed.
        await signIn.SignInAsync(user, isPersistent: false);
        return Results.LocalRedirect("/");
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext context, IAntiforgery antiforgery, SignInManager<User> signIn)
    {
        if (!await ValidateFormAsync(context, antiforgery))
            return Results.BadRequest("Invalid form submission. Reload the page and try again.");

        await signIn.SignOutAsync();
        return Results.LocalRedirect("/");
    }

    private static bool IsValidEmail(string email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= 256 &&
        !email.Any(char.IsWhiteSpace) && new EmailAddressAttribute().IsValid(email);

    private static async Task<bool> ValidateFormAsync(
        HttpContext context, IAntiforgery antiforgery)
    {
        if (!context.Request.HasFormContentType)
            return false;
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}
