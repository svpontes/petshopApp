using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace PetShopApp.Models;

/// <summary>
/// Represents one persisted order aggregate with customer details and purchase-time item snapshots.
/// JSON storage is the existing application design, not a limitation of SQLite relationships.
/// </summary>
public class Order
{
    private static readonly JsonSerializerOptions SnapshotReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // SQLite generates the order ID only when the aggregate is saved.
    public int Id { get; set; }

    // Nullable for legacy unassociated orders; the checkout service requires an authenticated owner.
    public int? UserId { get; set; }

    // New orders use UTC. Migrations do not reinterpret timestamps already stored in the database.
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Processing";

    // Purchase-time customer data remains independent of subsequent profile edits.
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";
    public string ShippingAddress { get; set; } = "";

    // Keep the raw persisted snapshot so status changes and migrations do not reserialize history.
    public string SerializedItems { get; set; } = "";

    /// <summary>
    /// Reads item snapshots without querying the current catalog.
    /// Each getter returns a detached list; assign a completed list to persist a changed snapshot.
    /// </summary>
    [NotMapped]
    public List<OrderItem> OrderItems
    {
        get
        {
            if (string.IsNullOrWhiteSpace(SerializedItems))
                return new List<OrderItem>();
            try
            {
                return JsonSerializer.Deserialize<List<OrderItem>>(SerializedItems, SnapshotReadOptions)
                    ?? new List<OrderItem>();
            }
            catch (JsonException)
            {
                // Preserve unreadable legacy payloads in SerializedItems for a separate data review.
                return new List<OrderItem>();
            }
        }
        set => SerializedItems = JsonSerializer.Serialize(value);
    }
}
