using Microsoft.EntityFrameworkCore;
using PetShopApp.Models;

namespace PetShopApp.Data
{
    /// <summary>
    /// The primary database context class that coordinates Entity Framework functionality 
    /// for the application's data models.
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the context using the specified options 
        /// (e.g., connection strings and database provider).
        /// </summary>
        public ApplicationDbContext(DbContextOptions options) : base(options)
        {
        }

        // --- DATABASE TABLES (DbSets) ---

        // Stores customer inquiries and support tickets from the contact form
        public DbSet<ContactMessage> ContactMessages { get; set; }

        // Stores historical order data, including serialized purchase items
        public DbSet<Order> Orders { get; set; }

        // Manages user accounts, credentials, and profile information
        public DbSet<User> Users { get; set; }

        // Stores email addresses for the newsletter mailing list
        public DbSet<NewsletterSubscriber> Subscribers { get; set; }

        // Manages customer-submitted ratings and comments for products
        public DbSet<ProductReview> ProductReviews { get; set; }

        // Manages the store's inventory, including prices, descriptions, and images
        public DbSet<Product> Products { get; set; }
    }
}