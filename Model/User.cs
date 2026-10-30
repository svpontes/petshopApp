namespace PetShopApp.Models
{
    /// <summary>
    /// Represents a user entity within the application, handling both 
    /// authentication data and personal profile information.
    /// </summary>
    public class User
    {
        // Primary Key for the Users table
        public int Id { get; set; }

        // Full name of the user
        public string Name { get; set; } = string.Empty;

        // Unique identifier for login purposes
        public string Email { get; set; } = string.Empty;

        // User password (stored as plain text for this implementation, 
        // though hashing is recommended for production)
        public string Password { get; set; } = string.Empty; 

        // Optional URL for the user's profile picture
        public string? AvatarUrl { get; set; }
     
        // Authorization role (e.g., "Customer" or "Admin") to manage permissions
        public string Role { get; set; } = "Customer"; 

        // --- EXTENDED PROFILE INFORMATION ---
        // These nullable properties allow for optional data entry during registration or checkout
        
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        
        // ZipCode property mapped to match checkout and database requirements
        public string? ZipCode { get; set; }
        
        public string? Country { get; set; }

        // --- RELATIONSHIPS ---

        /// <summary>
        /// Navigation property representing the collection of orders placed by the user.
        /// This establishes a One-to-Many relationship between User and Orders.
        /// </summary>
        public List<Order> OrderHistory { get; set; } = new List<Order>();
        
        // --- AUDIT METADATA ---

        // Automatically captures the timestamp when the user account is created
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}