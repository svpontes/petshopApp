using System.ComponentModel.DataAnnotations;

namespace PetShopApp.Models
{
    /// <summary>
    /// Represents a product category, such as Food, Accessories, or Toys.
    /// Categories can be shared across different animal types.
    /// </summary>
    public class Category
    {
        // Unique identifier for the category.
        public int Id { get; set; }

        // Category name displayed in the product catalog.
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;
    }
}