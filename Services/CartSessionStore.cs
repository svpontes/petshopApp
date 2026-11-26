using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using PetShopApp.Data;

namespace PetShopApp.Services;

/// <summary>
/// Preserves cart selections for the current owner and prevents sharing
/// between different customer accounts.
/// </summary>
public sealed class CartSessionStore(
    ProtectedSessionStorage storage,
    CartService cart,
    IDbContextFactory<ApplicationDbContext> dbFactory,
    AuthService authService)
{
    private const string StorageKey = "PetShopApp.Cart.v2";
    private const string LegacyStorageKey = "PetShopApp.Cart.v1";

    private readonly SemaphoreSlim operationLock = new(1, 1);
    private bool initialized;
    private int? ownerId;

    // Store selections without trusting prices supplied by browser storage.
    public sealed record StoredLine(
        int ProductId,
        string Size,
        int Quantity);

    // A null owner identifies a cart created by an anonymous visitor.
    public sealed record StoredCart(
        int? OwnerId,
        List<StoredLine> Lines);

    /// <summary>
    /// Restores this account's cart or adopts an anonymous visitor's selections.
    /// Discards selections belonging to another authenticated account.
    /// </summary>
    public async Task RestoreAsync()
    {
        await operationLock.WaitAsync();

        try
        {
            initialized = false;
            var currentOwner = authService.CurrentUser?.Id;

            // Legacy payloads have no ownership information.
            await storage.DeleteAsync(LegacyStorageKey);

            var saved = await ReadAsync();

            var canRestore = saved is not null &&
                (saved.OwnerId == currentOwner ||
                 (saved.OwnerId is null && currentOwner is not null));

            // Clear existing circuit state before restoring authorized selections.
            cart.ClearCart();
            ownerId = currentOwner;

            if (canRestore && saved!.Lines is { Count: > 0 })
            {
                await RestoreLinesAsync(saved.Lines);
            }

            // Reject items if authentication changed during database loading.
            if (authService.CurrentUser?.Id != currentOwner)
            {
                cart.ClearCart();
                ownerId = authService.CurrentUser?.Id;
            }

            // Persist guest adoption with the authenticated account identifier.
            await WriteAsync();
            initialized = true;
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <summary>
    /// Saves selections without assigning another account's items to the user.
    /// </summary>
    public async Task SaveAsync()
    {
        await operationLock.WaitAsync();

        try
        {
            // Avoid overwriting stored selections before initialization.
            if (!initialized)
            {
                return;
            }

            var currentOwner = authService.CurrentUser?.Id;

            if (ownerId != currentOwner)
            {
                cart.ClearCart();
                ownerId = currentOwner;
            }

            await WriteAsync();
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <summary>
    /// Clears visible and persisted selections before the logout request.
    /// Serializes operations so queued saves cannot restore previous items.
    /// </summary>
    public async Task ClearAsync()
    {
        await operationLock.WaitAsync();

        try
        {
            ownerId = authService.CurrentUser?.Id;
            cart.ClearCart();

            await storage.DeleteAsync(LegacyStorageKey);
            await WriteAsync();

            initialized = true;
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <summary>
    /// Reads protected state and safely discards unreadable payloads.
    /// </summary>
    private async Task<StoredCart?> ReadAsync()
    {
        try
        {
            var result = await storage.GetAsync<StoredCart>(StorageKey);
            return result.Success ? result.Value : null;
        }
        catch (Exception exception) when (
            exception is CryptographicException or
            JsonException or
            FormatException)
        {
            await storage.DeleteAsync(StorageKey);
            return null;
        }
    }

    /// <summary>
    /// Restores available products and options using current database prices.
    /// CartService applies the current promotion.
    /// </summary>
    private async Task RestoreLinesAsync(List<StoredLine> lines)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var ids = lines
            .Select(line => line.ProductId)
            .Distinct()
            .ToArray();

        var products = await db.Products
            .AsNoTracking()
            .Include(product => product.Variants)
            .Where(product => ids.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id);

        foreach (var line in lines)
        {
            if (line.Quantity <= 0 ||
                !products.TryGetValue(line.ProductId, out var product))
            {
                continue;
            }

            var variant = product.Variants
                .FirstOrDefault(option => option.Label == line.Size);

            if (product.Variants.Count > 0 && variant is null)
            {
                continue;
            }

            // Reject options that no longer belong to the product.
            if (product.Variants.Count == 0 &&
                (product.AvailableSizes.Count > 0
                    ? !product.AvailableSizes.Contains(line.Size)
                    : !string.IsNullOrEmpty(line.Size)))
            {
                continue;
            }

            cart.AddToCart(
                product,
                line.Size,
                line.Quantity,
                variant);
        }
    }

    /// <summary>
    /// Writes one protected snapshot containing the owner and selected items.
    /// </summary>
    private Task WriteAsync()
    {
        var lines = cart.CartItems
            .Select(item => new StoredLine(
                item.Product.Id,
                item.Size,
                item.Quantity))
            .ToList();

        return storage
            .SetAsync(StorageKey, new StoredCart(ownerId, lines))
            .AsTask();
    }
}