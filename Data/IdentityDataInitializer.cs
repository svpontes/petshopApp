using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PetShopApp.Models;

namespace PetShopApp.Data;

/// <summary>
/// Creates application roles and prepares legacy accounts for Identity sign-in.
/// </summary>
public static class IdentityDataInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var users = services.GetRequiredService<UserManager<User>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("IdentityInitialization");

        var accounts = await db.Users
            .OrderBy(user => user.Id)
            .ToListAsync();

        // Validate account identities before making any changes.
        foreach (var account in accounts)
        {
            var email = account.Email.Trim();

            if (string.IsNullOrWhiteSpace(email) ||
                email.Any(char.IsWhiteSpace) ||
                !new EmailAddressAttribute().IsValid(email))
            {
                throw new InvalidOperationException(
                    $"Account {account.Id} has an invalid email address. Resolve it before migrating accounts.");
            }
        }

        // Stop on ambiguous accounts without deleting or merging customer data.
        var duplicateEmails = accounts
            .GroupBy(
                user => users.NormalizeEmail(user.Email.Trim()),
                StringComparer.Ordinal)
            .Any(group => group.Count() > 1);

        if (duplicateEmails)
        {
            throw new InvalidOperationException(
                "Some accounts have the same normalized email. Resolve duplicates before migrating accounts.");
        }

        var duplicateNames = accounts
            .GroupBy(
                user => users.NormalizeName(GetUserName(user)),
                StringComparer.Ordinal)
            .Any(group => group.Count() > 1);

        if (duplicateNames)
        {
            throw new InvalidOperationException(
                "Some accounts have the same normalized username. Resolve duplicates before migrating accounts.");
        }

        // Keep role creation and account preparation in one transaction.
        await using var transaction = await db.Database.BeginTransactionAsync();

        foreach (var roleName in new[] { "Customer", "Admin" })
        {
            if (!await roles.RoleExistsAsync(roleName))
            {
                EnsureSucceeded(
                    await roles.CreateAsync(new IdentityRole<int>(roleName)),
                    $"Create role {roleName}");
            }
        }

        var migratedCount = 0;

        foreach (var account in accounts)
        {
            var needsMigration =
                string.IsNullOrWhiteSpace(account.UserName) ||
                string.IsNullOrWhiteSpace(account.NormalizedUserName) ||
                string.IsNullOrWhiteSpace(account.NormalizedEmail) ||
                string.IsNullOrWhiteSpace(account.SecurityStamp) ||
                string.IsNullOrWhiteSpace(account.ConcurrencyStamp);

            // Preserve Identity memberships, including deliberate role removals.
            if (!needsMigration)
            {
                continue;
            }

            var memberships = await users.GetRolesAsync(account);
            var legacyRole = memberships.Count == 0
                ? GetLegacyRole(account)
                : null;

            account.UserName = GetUserName(account);
            account.Email = account.Email.Trim().ToLowerInvariant();
            account.LockoutEnabled = true;

            if (legacyRole is not null)
            {
                account.Role = legacyRole;
            }

            if (string.IsNullOrWhiteSpace(account.SecurityStamp))
            {
                account.SecurityStamp = Guid.NewGuid().ToString("N");
            }

            if (string.IsNullOrWhiteSpace(account.ConcurrencyStamp))
            {
                account.ConcurrencyStamp = Guid.NewGuid().ToString("N");
            }

            // Populate normalized fields without replacing the password hash.
            EnsureSucceeded(
                await users.UpdateAsync(account),
                $"Prepare account {account.Id}");

            // Import the legacy role only when preparing an existing account.
            if (legacyRole is not null)
            {
                EnsureSucceeded(
                    await users.AddToRoleAsync(account, legacyRole),
                    $"Import role for account {account.Id}");
            }

            migratedCount++;
        }

        await transaction.CommitAsync();

        logger.LogInformation(
            "Identity roles ready. Prepared {MigratedCount} existing accounts.",
            migratedCount);
    }

    /// <summary>
    /// Preserves an existing username or derives one from the email address.
    /// </summary>
    private static string GetUserName(User account)
    {
        return string.IsNullOrWhiteSpace(account.UserName)
            ? account.Email.Trim().ToLowerInvariant()
            : account.UserName.Trim();
    }

    /// <summary>
    /// Converts supported legacy roles to their canonical Identity names.
    /// </summary>
    private static string GetLegacyRole(User account)
    {
        if (string.Equals(
            account.Role?.Trim(),
            "Customer",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Customer";
        }

        if (string.Equals(
            account.Role?.Trim(),
            "Admin",
            StringComparison.OrdinalIgnoreCase))
        {
            return "Admin";
        }

        throw new InvalidOperationException(
            $"Account {account.Id} has an unsupported legacy role. Resolve it before migrating accounts.");
    }

    /// <summary>
    /// Stops initialization when an Identity operation fails.
    /// </summary>
    private static void EnsureSucceeded(
        IdentityResult result,
        string operation)
    {
        if (!result.Succeeded)
        {
            var codes = string.Join(
                ", ",
                result.Errors.Select(error => error.Code));

            throw new InvalidOperationException(
                $"{operation} failed: {codes}.");
        }
    }
}