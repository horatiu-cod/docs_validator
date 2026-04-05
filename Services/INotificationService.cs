namespace DocsValidator.Services;

using DocsValidator.Models;

public interface INotificationService
{
    Task<bool> SendWelcomeEmailAsync(string email, string username);
    Task<bool> SendPasswordResetEmailAsync(string email, string resetToken);
    Task<bool> SendDocumentApprovedEmailAsync(string email, string documentName);
    Task<bool> SendNotificationAsync(Notification notification);
}
