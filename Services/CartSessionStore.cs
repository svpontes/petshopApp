using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;
using PetShopApp.Data;

namespace PetShopApp.Services;

/// <summary>
/// Preserves cart selections across HTTP sign-in and reloads authoritative prices.
/// </summary>
public sealed class CartSessionStore(
    ProtectedSessionStorage storage,
    CartService cart,
    IDbContextFactory<ApplicationDbContext> dbFactory)
{
    private const string StorageKey = "PetShopApp.Cart.v1";
    private readonly SemaphoreSlim saveLock = new(1, 1);
    public sealed record StoredLine(int ProductId, string Size, int Quantity);

    /// <summary>
    /// Restores available products and options using current promotional prices.
    /// </summary>
    public async Task RestoreAsync()
    {
        List<StoredLine>? lines;
        try
        {
            var result = await storage.GetAsync<List<StoredLine>>(StorageKey);
            lines = result.Success ? result.Value : null;
        }
        catch (Exception exception) when (
            exception is CryptographicException or JsonException)
        {
            // An unreadable payload is not a source of cart data.
            await storage.DeleteAsync(StorageKey);
            return;
        }

        if (lines is null || lines.Count == 0)
            return;

        await using var db = await dbFactory.CreateDbContextAsync();
        var ids = lines.Select(line => line.ProductId).Distinct().ToArray();
        var products = await db.Products.AsNoTracking()
            .Include(product => product.Variants)
            .Where(product => ids.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id);

        foreach (var line in lines)
        {
            if (line.Quantity <= 0 || !products.TryGetValue(line.ProductId, out var product))
                continue;

            var variant = product.Variants.FirstOrDefault(option => option.Label == line.Size);
            if (product.Variants.Count > 0 && variant is null)
                continue;

            if (product.Variants.Count == 0 &&
                (product.AvailableSizes.Count > 0
                    ? !product.AvailableSizes.Contains(line.Size)
                    : !string.IsNullOrEmpty(line.Size)))
                continue;

            cart.AddToCart(product, line.Size, line.Quantity, variant);
        }
    }

    /// <summary>
    /// Serializes writes and snapshots the latest cart after obtaining the lock.
    /// </summary>
    public async Task SaveAsync()
    {
        await saveLock.WaitAsync();
        try
        {
            var lines = cart.CartItems.Select(item =>
                new StoredLine(item.Product.Id, item.Size, item.Quantity)).ToList();
            await storage.SetAsync(StorageKey, lines);
        }
        finally
        {
            saveLock.Release();
        }
    }
}