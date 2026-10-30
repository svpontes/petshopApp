using System.ComponentModel.DataAnnotations.Schema;

namespace PetShopApp.Models
{
    /// <summary>
    /// Represents a product in the catalog, including its pricing,
    /// descriptions, and image management logic.
    /// </summary>
    public class Product
    {
        // Unique identifier for the product (Primary Key)
        public int Id { get; set; }

        // The display name of the product
        public string Name { get; set; } = string.Empty;

        // Detailed marketing or technical description of the product
        public string Description { get; set; } = string.Empty;

        // Product category, such as Clothing, Mugs, or Books
        public string Category { get; set; } = string.Empty;

        // Retail price of the item
        public decimal Price { get; set; }

        // Main thumbnail or primary image URL
        public string ImageUrl { get; set; } = string.Empty;

        /// <summary>
        /// Raw string storage for secondary images.
        /// Since SQLite does not natively support lists, multiple URLs are
        /// stored as a single string separated by newline characters.
        /// </summary>
        public string SecondaryImageUrlsRaw { get; set; } = string.Empty;

        // List of sizes (e.g., S, M, L, XL) available for the product.
        // [NotMapped] prevents Entity Framework from creating a column for this list.
        [NotMapped]
        public List<string> AvailableSizes { get; set; } = new();

        /// <summary>
        /// Logic-driven property to handle secondary image URLs.
        /// It acts as a bridge between the raw database string and a C# List.
        /// [NotMapped] ensures this helper property is ignored during database migrations.
        /// </summary>
        [NotMapped]
        public List<string> SecondaryImageUrls
        {
            get => string.IsNullOrWhiteSpace(SecondaryImageUrlsRaw)
                ? new List<string>()
                : SecondaryImageUrlsRaw
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

            set => SecondaryImageUrlsRaw = value == null || !value.Any()
                ? string.Empty
                : string.Join("\n", value);
        }
    }
}