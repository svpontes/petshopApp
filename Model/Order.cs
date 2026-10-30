using System.Text.Json;
using System.ComponentModel.DataAnnotations.Schema;

namespace PetShopApp.Models
{
    /// <summary>
    /// Represents a completed customer order, storing transaction details, 
    /// shipping info, and a serialized snapshot of purchased items.
    /// </summary>
    public class Order
    {
        // Unique identifier for the Order (Primary Key)
        public int Id { get; set; }

        // Timestamp of when the order was successfully placed
        public DateTime OrderDate { get; set; } = DateTime.Now;

        // Total monetary value of the transaction
        public decimal TotalAmount { get; set; }

        // Current fulfillment status (e.g., "Processing", "Shipped", "Delivered")
        public string Status { get; set; } = "Processing";

        // Customer contact information for shipping and communication
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;

        /// <summary>
        /// Stores the collection of purchased items as a JSON string.
        /// This approach bypasses SQLite's limitation regarding nested collections 
        /// by persisting the items as a single text block.
        /// </summary>
        public string SerializedItems { get; set; } = string.Empty;

        /// <summary>
        /// Helper property for the UI and Business Logic.
        /// It handles the serialization and deserialization of OrderItems on the fly.
        /// [NotMapped] tells Entity Framework Core to ignore this property in the database schema.
        /// </summary>
        [NotMapped]
        public List<OrderItem> OrderItems 
        { 
            get 
            {
                // Returns an empty list if there is no data to deserialize
                if (string.IsNullOrWhiteSpace(SerializedItems)) return new List<OrderItem>();
                
                try 
                {
                    // Deserializes the JSON string into a C# List.
                    // PropertyNameCaseInsensitive ensures compatibility regardless of JSON casing.
                    return JsonSerializer.Deserialize<List<OrderItem>>(SerializedItems, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<OrderItem>();
                }
                catch 
                { 
                    // Fail-safe: returns an empty list if the JSON is malformed
                    return new List<OrderItem>(); 
                }
            }
            
            set 
            {
                // Converts the List back into a JSON string for database storage
                SerializedItems = JsonSerializer.Serialize(value);
            }
        }
    }
}