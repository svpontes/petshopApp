using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PetShopApp.Data;
using PetShopApp.Models;

namespace PetShopApp.Services;

/// <summary>
/// Restores the account from Identity. HTTP endpoints own cookie sign-in and sign-out.
/// </summary>
public sealed class AuthService(
    IDbContextFactory<ApplicationDbContext> dbFactory,
    IOptions<IdentityOptions> identityOptions)
{
    public User? CurrentUser { get; private set; }
    public bool IsLoggedIn => CurrentUser is not null;
    public bool IsAdmin { get; private set; }
    public event Action? OnChange;
    /// <summary>
    /// Rechecks the current account and optional administrator role in the database.
    /// Call this before protected queries and mutations, not only when a page opens.
    /// </summary>
    public async Task<bool> IsAuthorizedAsync(bool requireAdmin = false)
    {
        var account = CurrentUser;
        var stamp = account?.SecurityStamp;

        // The circuit must already have been initialized from a valid Identity principal.
        if (account is null || string.IsNullOrEmpty(stamp) ||
            (requireAdmin && !IsAdmin))
        {
            return false;
        }

        await using var db = await dbFactory.CreateDbContextAsync();

        // Read fresh data so deletion, stamp changes and revoked roles take effect.
        var allowed = await db.Users.AsNoTracking().AnyAsync(user =>
            user.Id == account.Id && user.SecurityStamp == stamp &&
            (!requireAdmin || db.UserRoles.Any(membership =>
                membership.UserId == user.Id && db.Roles.Any(role =>
                    role.Id == membership.RoleId && role.Name == "Admin"))));

        // An awaited query must not authorize a different or cleared circuit account.
        return allowed && ReferenceEquals(CurrentUser, account) &&
            (!requireAdmin || IsAdmin);
    }
    /// <summary>
    /// Loads the account before pages evaluate their existing session guards.
    /// </summary>
    public async Task InitializeAsync(ClaimsPrincipal principal)
    {
        ClearSession(notify: false);
        var claims = identityOptions.Value.ClaimsIdentity;
        if (principal.Identity?.IsAuthenticated != true ||
            !int.TryParse(principal.FindFirstValue(claims.UserIdClaimType), out var id))
        {
            OnChange?.Invoke();
            return;
        }

        await using var db = await dbFactory.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking()
            .Include(account => account.OrderHistory)
            .SingleOrDefaultAsync(account => account.Id == id);

        // Deleted accounts and changed security stamps cannot restore a session.
        if (user is null || string.IsNullOrEmpty(user.SecurityStamp) ||
            principal.FindFirstValue(claims.SecurityStampClaimType) != user.SecurityStamp)
        {
            OnChange?.Invoke();
            return;
        }

        var roles = await (from membership in db.UserRoles
                           join role in db.Roles on membership.RoleId equals role.Id
                           where membership.UserId == id
                           select role.Name).ToListAsync();

        CurrentUser = user;
        IsAdmin = principal.IsInRole("Admin") && roles.Contains("Admin");
        OnChange?.Invoke();
    }

    /// <summary>
    /// Clears circuit data; the logout endpoint separately removes the cookie.
    /// </summary>
    public void ClearSession(bool notify = true)
    {
        CurrentUser = null;
        IsAdmin = false;
        if (notify)
            OnChange?.Invoke();
    }
}
