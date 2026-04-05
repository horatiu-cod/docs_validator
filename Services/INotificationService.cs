namespace DocsValidator.Services;

using DocsValidator.Models;

public interface INotificationService
{
    Task<bool> SendWelcomeEmailAsync(string email, string username);
    Task<bool> SendPasswordResetEmailAsync(string email, string resetToken);
    Task<bool> SendDocumentApprovedEmailAsync(string email, string documentName);
    Task<bool> SendDocumentApprovedEmailWithAttachmentAsync(string email, string documentName, string attachmentPath, string? overrideRecipient = null);
    Task<bool> SendNotificationAsync(Notification notification);
}
