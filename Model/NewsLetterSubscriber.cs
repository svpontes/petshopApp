namespace PetShopApp.Models;

/// <summary>
/// Represents a user who has opted into the marketing newsletter.
/// Used for lead generation and email marketing campaigns.
/// </summary>
public class NewsletterSubscriber
{
    // Unique identifier for the subscriber record (Primary Key)
    public int Id { get; set; }

    // The verified email address of the subscriber
    public string Email { get; set; } = string.Empty;

    // The date and time when the user joined the mailing list.
    // Defaults to the current system time upon instantiation.
    public DateTime SubscribedAt { get; set; } = DateTime.Now;
}