using PetShopApp.Models;

namespace PetShopApp.Services;

/// <summary>
/// Applies the same stock rules to cards, cart lines and product details.
/// Cart selections do not reserve or subtract physical inventory.
/// </summary>
public static class ProductStock
{
    /// <summary>
    /// Returns stock for the selected priced variant or the shared product pool.
    /// A missing variant or invalid legacy option is unavailable.
    /// </summary>
    public static int Quantity(Product product, string? option = null)
    {
        if (product.Variants.Count > 0)
            return Math.Max(0, product.Variants
                .FirstOrDefault(variant => variant.Label == option)?.StockQuantity ?? 0);

        if (product.AvailableSizes.Count > 0 &&
            option is not null && !product.AvailableSizes.Contains(option))
            return 0;

        return Math.Max(0, product.StockQuantity);
    }

    /// <summary>
    /// A product is sold out only when none of its purchasable variants has stock.
    /// </summary>
    public static bool IsSoldOut(Product product) => product.Variants.Count > 0
        ? product.Variants.All(variant => variant.StockQuantity <= 0)
        : product.StockQuantity <= 0;
}

/// <summary>
/// Provides a safe checkout message without exposing database errors.
/// </summary>
public sealed class StockUnavailableException(string message)
    : InvalidOperationException(message);
