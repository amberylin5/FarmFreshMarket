using FarmFreshMarket.Models;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace FarmFreshMarket.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string body);
        Task SendPasswordResetEmailAsync(string email, string resetLink);
    }

    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;
        private readonly IOptions<EmailSettings> _emailSettings;

        public EmailService(ILogger<EmailService> logger, IOptions<EmailSettings> emailSettings = null)
        {
            _logger = logger;
            _emailSettings = emailSettings;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            try
            {
                // Check if email settings are available
                if (_emailSettings?.Value == null)
                {
                    Console.WriteLine($"\n📧 EMAIL SIMULATION (Settings not configured):");
                    Console.WriteLine($"To: {toEmail}");
                    Console.WriteLine($"Subject: {subject}");
                    Console.WriteLine($"Body Preview: {body.Substring(0, Math.Min(100, body.Length))}...");
                    Console.WriteLine($"\n📋 FOR TESTING: Check console for email content\n");
                    return;
                }

                var settings = _emailSettings.Value;

                Console.WriteLine($"\n🔍 EMAIL SERVICE - SENDING:");
                Console.WriteLine($"To: {toEmail}");
                Console.WriteLine($"Subject: {subject}");
                Console.WriteLine($"SMTP Server: {settings.SmtpServer}:{settings.SmtpPort}");

                // If SMTP server is not configured, use simulation
                if (string.IsNullOrEmpty(settings.SmtpServer) ||
                    string.IsNullOrEmpty(settings.SenderEmail))
                {
                    Console.WriteLine("⚠️ SMTP not fully configured - using simulation mode");
                    Console.WriteLine($"\n📧 EMAIL SIMULATION:");
                    Console.WriteLine($"To: {toEmail}");
                    Console.WriteLine($"Subject: {subject}");
                    Console.WriteLine($"Body: {body.Substring(0, Math.Min(200, body.Length))}...\n");
                    return;
                }

                // Create email message
                var mailMessage = new MailMessage
                {
                    From = new MailAddress(settings.SenderEmail, "Fresh Farm Market"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(toEmail);

                Console.WriteLine("📧 Email message created...");

                // Create SMTP client
                using var smtpClient = new SmtpClient(settings.SmtpServer, settings.SmtpPort)
                {
                    Credentials = new NetworkCredential(settings.SenderEmail, settings.SenderPassword),
                    EnableSsl = settings.EnableSsl,
                    Timeout = 10000
                };

                Console.WriteLine("📤 Sending email...");
                await smtpClient.SendMailAsync(mailMessage);
                Console.WriteLine($"✅ EMAIL SENT SUCCESSFULLY TO: {toEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ EMAIL SENDING FAILED:");
                Console.WriteLine($"Error: {ex.Message}");

                // Fallback to simulation
                Console.WriteLine($"\n📧 EMAIL SIMULATION (Fallback):");
                Console.WriteLine($"To: {toEmail}");
                Console.WriteLine($"Subject: {subject}");
                Console.WriteLine($"Body Preview: {body.Substring(0, Math.Min(200, body.Length))}...\n");
            }
        }

        public async Task SendPasswordResetEmailAsync(string email, string resetLink)
        {
            var subject = "Password Reset Request - Fresh Farm Market";
            var body = $@"
            <h2>Password Reset Request</h2>
            <p>You requested to reset your password for <strong>Fresh Farm Market</strong>.</p>
            <p>Click the link below to reset your password:</p>
            <p><a href='{resetLink}'>Reset Password</a></p>
            <p>Or copy and paste this link in your browser:</p>
            <p><code>{resetLink}</code></p>
            <p>This link will expire in 24 hours.</p>
            <p>If you didn't request this, please ignore this email.</p>
            <hr>
            <p><strong>Fresh Farm Market Team</strong></p>";

            await SendEmailAsync(email, subject, body);
        }
    }
}