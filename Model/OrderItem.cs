namespace PetShopApp.Models
{
    /// <summary>
    /// Represents a specific product within a completed order.
    /// This class stores a snapshot of the product's data at the moment of purchase.
    /// </summary>
    public class OrderItem
    {
        // Unique identifier for the order item entry (Primary Key)
        public int Id { get; set; }

        // Reference to the original product ID for inventory or analytics tracking
        public int ProductId { get; set; }

        // Duplicated product name to ensure the order history remains accurate 
        // even if the original product name is changed in the catalog later.
        public string ProductName { get; set; } = string.Empty;

        // Stores the image URL used at the time of purchase to maintain visual history
        public string ImageUrl { get; set; } = string.Empty;

        // The exact price the customer paid at the time of checkout.
        // This prevents historical orders from changing if the catalog price is updated.
        public decimal Price { get; set; }

        // Number of units purchased for this specific item
        public int Quantity { get; set; }

        // The specific size selected by the customer (e.g., S, M, L, XL)
        public string Size { get; set; } = string.Empty;
    }
}