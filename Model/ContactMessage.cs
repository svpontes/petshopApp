/// <summary>
/// Represents a message sent by a user through the website's contact form.
/// Used for customer support inquiries and general communication.
/// </summary>
public class ContactMessage
{
    // Unique identifier for the contact message entry (Primary Key)
    public int Id { get; set; }

    // The name of the person sending the message
    public string Name { get; set; } = string.Empty;

    // The email address of the sender for follow-up communication
    public string Email { get; set; } = string.Empty;

    // The topic or category of the inquiry
    public string Subject { get; set; } = string.Empty;

    // The full textual content of the user's message
    public string Message { get; set; } = string.Empty;

    // Timestamp capturing exactly when the message was submitted.
    // Defaults to the current system date and time.
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}