namespace DocsValidator.Models;

public class Notification
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum NotificationType { WelcomeEmail, PasswordReset, DocumentApproved, Generic }
public enum NotificationStatus { Pending, Sent, Failed }
