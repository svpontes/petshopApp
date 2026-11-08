using System.ComponentModel.DataAnnotations;

namespace PetShopApp.Models
{
    /// <summary>
    /// Represents an animal type used to organize the product catalog.
    /// Examples: Dog, Cat, Horse, Rabbit, Bird, Sheep, and Pig.
    /// </summary>
    public class AnimalType
    {
        // Unique identifier for the animal type.
        public int Id { get; set; }

        // Display name shown in the animal selection menu.
        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;
    }
}