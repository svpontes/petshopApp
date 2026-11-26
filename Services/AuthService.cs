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
