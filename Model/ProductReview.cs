namespace PetShopApp.Models
{
    /// <summary>
    /// Represents a customer review for a specific product, 
    /// storing ratings, comments, and reviewer identification.
    /// </summary>
    public class ProductReview
    {
        // Unique identifier for the review entry (Primary Key)
        public int Id { get; set; }

        // Foreign Key linking the review to a specific Product
        public int ProductId { get; set; }

        // Email of the user who wrote the review (used for identification)
        public string UserEmail { get; set; } = string.Empty;

        // Display name of the user who authored the review
        public string UserName { get; set; } = string.Empty;

        // Numerical rating provided by the user (e.g., scale of 1 to 5)
        public int Rating { get; set; } 

        // Textual content of the user's feedback
        public string Comment { get; set; } = string.Empty;

        // Timestamp of when the review was submitted
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}