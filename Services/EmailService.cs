using System;
using System.Threading.Tasks;
using DocsValidator.Models;
using DocsValidator.Settings;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.Extensions.Options;

namespace DocsValidator.Services
{
    public class EmailService : INotificationService
    {
        private readonly EmailSettings _settings;
        public EmailService(IOptions<EmailSettings> settings)
        {
            _settings = settings.Value;
        }

        public async Task<bool> SendWelcomeEmailAsync(string email, string username)
        {
            var subject = "Welcome to Docs Validator!";
            var body = $"Hello {username},<br/>Welcome to Docs Validator.";
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendPasswordResetEmailAsync(string email, string resetToken)
        {
            var subject = "Password Reset";
            var body = $"Use this token to reset your password: {resetToken}";
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendDocumentApprovedEmailAsync(string email, string documentName)
        {
            var subject = "Document Approved";
            var body = $"Your document '{documentName}' has been approved.";
            return await SendEmailAsync(email, subject, body);
        }

        public async Task<bool> SendNotificationAsync(Notification notification)
        {
            return await SendEmailAsync(notification.Email, notification.Subject, notification.Body);
        }

        private async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.Smtp.FromName, _settings.Smtp.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = body };

            try
            {
                using var client = new SmtpClient();
                await client.ConnectAsync(_settings.Smtp.Host, _settings.Smtp.Port, _settings.Smtp.UseTls);
                await client.AuthenticateAsync(_settings.Smtp.Username, _settings.Smtp.Password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
                return true;
            }
            catch (Exception)
            {
                // TODO: Add logging
                return false;
            }
        }
    }
}
