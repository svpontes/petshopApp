using PetShopApp.Models;

namespace PetShopApp.Services;

/// <summary>
/// Stores selections and limits quantities against the latest loaded inventory.
/// Adding or removing items never changes database stock; checkout is authoritative.
/// </summary>
public class CartService
{
    private readonly List<CartItem> items = new();
    public event Action? OnChange;
    public IReadOnlyList<CartItem> CartItems => items.AsReadOnly();
    public decimal UnitPrice { get; set; }
    public int TotalItems => items.Sum(item => item.Quantity);
    public decimal TotalPrice => items.Sum(item => item.UnitPrice * item.Quantity);
    public string LastError { get; private set; } = "";

    /// <summary>
    /// Counts units already selected from the same physical stock pool.
    /// Legacy CSV options share one pool; priced variants do not.
    /// </summary>
    public int RemainingQuantity(Product product, string? option = null)
    {
        var selected = items.Where(item => item.Product.Id == product.Id &&
            (product.Variants.Count == 0 || item.Size == option))
            .Sum(item => (long)item.Quantity);
        return (int)Math.Max(0, ProductStock.Quantity(product, option) - selected);
    }

    /// <summary>
    /// Refreshes availability without silently discarding selected quantities.
    /// The checkout service independently verifies the database again.
    /// </summary>
    public void RefreshProduct(Product product)
    {
        foreach (var item in items.Where(item => item.Product.Id == product.Id))
            item.Product = product;
        OnChange?.Invoke();
    }

    /// <summary>
    /// Adds only valid options and quantities; failure leaves every cart line unchanged.
    /// Uses the variant owned by the product rather than a supplied price or stock value.
    /// </summary>
    public bool AddToCart(Product product, string size, int quantity, ProductVariant? variant = null)
    {
        if (product is null || quantity <= 0)
            return Fail("Choose a valid product and quantity.");

        var option = variant?.Label ?? size ?? "";
        var savedVariant = product.Variants.FirstOrDefault(item => item.Label == option);
        if ((variant is not null && (variant.ProductId != product.Id || savedVariant?.Id != variant.Id)) ||
            (product.Variants.Count > 0 && savedVariant is null) ||
            (product.Variants.Count == 0 && product.AvailableSizes.Count > 0 && !product.AvailableSizes.Contains(option)) ||
            (product.Variants.Count == 0 && product.AvailableSizes.Count == 0 && option.Length > 0))
            return Fail("This product option is no longer available.");

        // Replace older snapshots before checking the combined cart quantity.
        RefreshProduct(product);
        if (quantity > RemainingQuantity(product, option))
            return Fail(ProductStock.Quantity(product, option) == 0
                ? "OUT OF STOCK. Please choose another available product or option."
                : "The requested quantity exceeds the available stock, including items already in your bag.");

        var unitPrice = StorePromotion.GetSaleUnitPrice(savedVariant?.Price ?? product.Price);
        var existing = items.FirstOrDefault(item => item.Product.Id == product.Id && item.Size == option);
        if (existing is null)
            items.Add(new CartItem { Product = product, Size = option, Quantity = quantity, UnitPrice = unitPrice });
        else
        {
            existing.Quantity = checked(existing.Quantity + quantity);
            existing.UnitPrice = unitPrice;
        }
        LastError = "";
        OnChange?.Invoke();
        return true;
    }

    /// <summary>
    /// Limits increases but always permits reductions and removal of stale selections.
    /// </summary>
    public bool UpdateQuantity(int productId, string size, int change)
    {
        var item = items.FirstOrDefault(line => line.Product.Id == productId && line.Size == size);
        if (item is null)
            return false;
        var next = (long)item.Quantity + change;
        if (next > int.MaxValue || (change > 0 && change > RemainingQuantity(item.Product, item.Size)))
            return Fail("The requested quantity exceeds the available stock.");
        if (next <= 0)
            items.Remove(item);
        else
            item.Quantity = (int)next;
        LastError = "";
        OnChange?.Invoke();
        return true;
    }

    /// <summary>Removes a selection without changing physical inventory.</summary>
    public void RemoveFromCart(int productId, string size)
    {
        items.RemoveAll(item => item.Product.Id == productId && item.Size == size);
        LastError = "";
        OnChange?.Invoke();
    }

    /// <summary>Clears this account's selections without changing physical inventory.</summary>
    public void ClearCart()
    {
        items.Clear();
        LastError = "";
        OnChange?.Invoke();
    }

    // Failed additions are visible to every purchasing UI instead of reporting false success.
    private bool Fail(string message)
    {
        LastError = message;
        OnChange?.Invoke();
        return false;
    }
}

/// <summary>Stores a selection and its displayed promotional unit price.</summary>
public class CartItem
{
    public Product Product { get; set; } = new();
    public string Size { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
