using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PetShopApp.Models;

namespace PetShopApp.Data;

/// <summary>
/// Grants a role to an explicitly selected, existing local development account.
/// This command never creates accounts or changes their passwords.
/// </summary>
public static class DevelopmentAdminSetup
{
    public static async Task GrantAsync(
        IServiceProvider services,
        IHostEnvironment environment,
        string email)
    {
        // Refuse execution before opening a database outside Development.
        if (!environment.IsDevelopment())
            throw new InvalidOperationException(
                "Local administrator setup is available only in Development.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedEmail.Any(char.IsWhiteSpace) ||
            !new EmailAddressAttribute().IsValid(normalizedEmail))
        {
            throw new ArgumentException("Provide a valid registered email address.");
        }

        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("DevelopmentAdminSetup");

        // Require registration through the normal Identity workflow first.
        var user = await users.FindByEmailAsync(normalizedEmail)
            ?? throw new InvalidOperationException(
                "Register this local account before granting administrator access.");

        if (!await roles.RoleExistsAsync("Admin"))
            throw new InvalidOperationException(
                "Start the application normally once to initialize Identity roles.");

        // Repeating the command must not replace credentials or invalidate sessions again.
        if (await users.IsInRoleAsync(user, "Admin"))
        {
            logger.LogInformation("The selected local account already has administrator access.");
            return;
        }

        // Commit the membership and stamp change together, or preserve the original account.
        await using var transaction = await db.Database.BeginTransactionAsync();
        EnsureSucceeded(await users.AddToRoleAsync(user, "Admin"));
        user.Role = "Admin";
        EnsureSucceeded(await users.UpdateSecurityStampAsync(user));
        await transaction.CommitAsync();

        logger.LogInformation(
            "Local administrator access granted. Sign in again to refresh the account claims.");
    }

    /// <summary>
    /// Report operation codes without exposing credentials or database exception details.
    /// </summary>
    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "Local administrator setup failed: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
    }
}