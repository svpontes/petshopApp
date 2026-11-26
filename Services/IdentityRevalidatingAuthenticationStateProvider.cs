using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using PetShopApp.Models;

namespace PetShopApp.Services;

/// <summary>
/// Invalidates active circuits when an account, security stamp or role changes.
/// </summary>
public sealed class IdentityRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(1);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = scopeFactory.CreateAsyncScope();
        return await ValidatePrincipalAsync(scope.ServiceProvider, authenticationState.User);
    }

    /// <summary>
    /// Uses fresh Identity data for both cookie requests and active circuits.
    /// </summary>
    public static async Task<bool> ValidatePrincipalAsync(
        IServiceProvider services, ClaimsPrincipal principal)
    {
        var manager = services.GetRequiredService<UserManager<User>>();
        var user = await manager.GetUserAsync(principal);
        if (user is null || string.IsNullOrEmpty(user.SecurityStamp))
            return false;

        var claims = manager.Options.ClaimsIdentity;
        if (principal.FindFirst(claims.SecurityStampClaimType)?.Value != user.SecurityStamp)
            return false;

        // Changing a role invalidates stale claims instead of restoring old privileges.
        var roles = await manager.GetRolesAsync(user);
        var claimedRoles = principal.FindAll(claims.RoleClaimType).Select(claim => claim.Value);
        return roles.ToHashSet(StringComparer.Ordinal).SetEquals(claimedRoles);
    }
}
