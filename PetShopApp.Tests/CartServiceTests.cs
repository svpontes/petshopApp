// Import the application's product and variant models.
using PetShopApp.Models;

// Import the actual cart service being tested.
using PetShopApp.Services;

// Import xUnit's test attributes and assertions.
using Xunit;

namespace PetShopApp.Tests;

/// <summary>
/// Verifies the shopping cart's pricing and quantity behavior.
/// Each test creates an independent cart and uses only in-memory data.
/// No application startup or SQLite database is required.
/// </summary>
public sealed class CartServiceTests
{
    /// <summary>
    /// Verifies that a product without options can be purchased
    /// and receives the advertised 10% discount.
    /// </summary>
    [Fact]
    public void AddProductWithoutOptions_AppliesTenPercentDiscount()
    {
        // Arrange: Create an empty cart for this test.
        var cart = new CartService();

        // Define a product with a regular unit price of 10.00.
        // The expected promotional unit price is 9.00.
        var product = new Product
        {
            Id = 1,
            Name = "Cat collar",
            StockQuantity = 100,
            Price = 10m
        };

        // Act: Add two units without selecting a size or variant.
        cart.AddToCart(product, "", 2);

        // Assert: Both units must belong to one cart line.
        var item = Assert.Single(cart.CartItems);

        // Products without options must retain an empty option value.
        Assert.Equal("", item.Size);

        // The cart line must contain the requested quantity.
        Assert.Equal(2, item.Quantity);

        // The advertised discount must be applied once to the unit price.
        Assert.Equal(9m, item.UnitPrice);

        // The cart must report two individual units.
        Assert.Equal(2, cart.TotalItems);

        // Two units at 9.00 each must produce a total of 18.00.
        Assert.Equal(18m, cart.TotalPrice);
    }

    /// <summary>
    /// Verifies that selecting a variant uses its own price
    /// and applies the promotion to that variant price.
    /// </summary>
    [Fact]
    public void AddVariant_UsesDiscountedVariantPrice()
    {
        // Arrange: Create an independent cart.
        var cart = new CartService();

        // Use a base price different from the variant price.
        // This allows the test to detect incorrect base-price usage.
        var product = new Product
        {
            Id = 2,
            Name = "Dog food",
            StockQuantity = 100,
            Price = 100m
        };

        // Define a variant belonging to this product.
        // A regular price of 20.00 should become 18.00 after the discount.
        var variant = new ProductVariant
        {
            Id = 11,
            ProductId = product.Id,
            Product = product,
            Label = "2.2 lb",
            StockQuantity = 100,
            Price = 20m
        };

        // Represent the variant as an available option for the product.
        product.Variants.Add(variant);

        // Act: Add three units of the selected variant.
        cart.AddToCart(product, variant.Label, 3, variant);

        // Assert: The purchase must create one cart line.
        var item = Assert.Single(cart.CartItems);

        // The selected weight must be preserved.
        Assert.Equal("2.2 lb", item.Size);

        // The requested quantity must be preserved.
        Assert.Equal(3, item.Quantity);

        // The unit price must come from the discounted variant price.
        Assert.Equal(18m, item.UnitPrice);

        // Three units at 18.00 each must produce a total of 54.00.
        Assert.Equal(54m, cart.TotalPrice);
    }

    /// <summary>
    /// Verifies that adding the same product and option again
    /// increases the existing quantity without creating duplicate lines.
    /// </summary>
    [Fact]
    public void AddSameProductAndOption_IncreasesQuantityWithoutDuplicateLines()
    {
        // Arrange: Create an independent cart.
        var cart = new CartService();

        // Define the product used by both add operations.
        var product = new Product
        {
            Id = 3,
            Name = "Cat food",
            StockQuantity = 100,
            Price = 100m
        };

        // Define the same purchasable option for both operations.
        var variant = new ProductVariant
        {
            Id = 12,
            ProductId = product.Id,
            Product = product,
            Label = "2.2 lb",
            StockQuantity = 100,
            Price = 20m
        };

        // Associate the option with its product.
        product.Variants.Add(variant);

        // Act: Add one unit, then add two more units of the same option.
        cart.AddToCart(product, variant.Label, 1, variant);
        cart.AddToCart(product, variant.Label, 2, variant);

        // Assert: Repeated additions must leave only one cart line.
        var item = Assert.Single(cart.CartItems);

        // The combined line must retain the selected option.
        Assert.Equal("2.2 lb", item.Size);

        // The quantities must be combined: 1 + 2 = 3.
        Assert.Equal(3, item.Quantity);

        // The promotional unit price must remain correct.
        Assert.Equal(18m, item.UnitPrice);

        // The total item count must reflect the combined quantity.
        Assert.Equal(3, cart.TotalItems);

        // The combined quantity must produce the expected cart total.
        Assert.Equal(54m, cart.TotalPrice);
    }
}