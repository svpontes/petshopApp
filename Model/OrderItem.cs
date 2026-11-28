namespace PetShopApp.Models;

/// <summary>
/// Contains purchase-time item values serialized inside an order's historical snapshot.
/// This value object is not an EF entity and has no foreign key to the changing product catalog.
/// </summary>
public class OrderItem
{
    // Retain the legacy JSON member; it is not a database-generated order-item primary key.
    public int Id { get; set; }

    // Historical lookup ID used by Buy Again; removing a product must not remove this snapshot.
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string ImageUrl { get; set; } = "";

    // Store the actual purchased unit price and quantity rather than recomputing historical totals.
    public decimal Price { get; set; }
    public int Quantity { get; set; }

    // Preserve the selected variant/option label even if the live option is renamed or deleted.
    public string Size { get; set; } = "";
}
