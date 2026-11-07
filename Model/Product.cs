using System.ComponentModel.DataAnnotations.Schema;

namespace PetShopApp.Models
{
    /// <summary>
    /// Represents a product in the catalog.
    /// </summary>
    public class Product
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;
        // fild for more prouct info
        public string MoreInformation { get; set; } = string.Empty;

        // Foreign key identifying the animal type this product is intended for.
        public int AnimalTypeId { get; set; }

        // Navigation property providing access to the related animal type.
        public AnimalType AnimalType { get; set; } = null!;

        // Keep the existing text category while the catalog is being updated.
        public string Category { get; set; } = string.Empty;
        
        // Identifies the category linked to this product.
        public int? CategoryId {get; set;}
        // Provides access to the related category.
        public Category? ProductCategory { get; set; }
        
        public decimal Price { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        // Stores secondary image URLs separated by newlines.
        public string SecondaryImageUrlsRaw { get; set; } = string.Empty;

        public string AvailableOptionsRaw { get; set; } = string.Empty;

        [NotMapped]
        public List<string> AvailableSizes
        {
            get => AvailableOptionsRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            set => AvailableOptionsRaw = value is null
                ? string.Empty
                : string.Join(", ", value);
        }
        [NotMapped]
        public List<string> SecondaryImageUrls
        {
            get => string.IsNullOrWhiteSpace(SecondaryImageUrlsRaw)
                ? new List<string>()
                : SecondaryImageUrlsRaw
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();

            set => SecondaryImageUrlsRaw = value is null || value.Count == 0
                ? string.Empty
                : string.Join("\n", value);
        }
    }
}