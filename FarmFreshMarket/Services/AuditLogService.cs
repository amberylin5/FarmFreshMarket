using FarmFreshMarket.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text;

namespace FarmFreshMarket.Services
{
    public interface IAuditLogService
    {
        Task LogAsync(string userId, string userEmail, string action, string description, bool success = true, string additionalInfo = null);
        Task<List<AuditLog>> GetUserLogsAsync(string userId);
        Task<List<AuditLog>> GetRecentLogsAsync(int count = 100);
        Task<int> GetFailedLoginCountAsync(string userEmail, TimeSpan timeWindow);
    }

    public class AuditLogService : IAuditLogService
    {
        private readonly AuthDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(AuthDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<AuditLogService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        // Sanitize user input for logging
        private static string SanitizeForLog(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remove newlines and control characters that could forge log entries
            var sanitized = input
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("\t", " ")
                .Replace(Environment.NewLine, " ");

            // Remove any remaining control characters
            var result = new StringBuilder();
            foreach (char c in sanitized)
            {
                if (!char.IsControl(c))
                {
                    result.Append(c);
                }
                else
                {
                    result.Append(' ');
                }
            }

            // Truncate to prevent excessively long logs
            return result.Length > 200 ? result.ToString(0, 200) + "..." : result.ToString();
        }

        private string GetClientIP()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext == null) return "Unknown";

                var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();

                if (string.IsNullOrEmpty(ip))
                    ip = httpContext.Connection.RemoteIpAddress?.ToString();

                return ip ?? "Unknown";
            }
            catch
            {
                return "Unknown";
            }
        }

        private string GetUserAgent()
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var userAgent = httpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown";

                // Truncate to 500 characters to fit in database
                return userAgent.Length > 500 ? userAgent.Substring(0, 500) : userAgent;
            }
            catch
            {
                return "Unknown";
            }
        }

        public async Task LogAsync(string userId, string userEmail, string action, string description, bool success = true, string? additionalInfo = null)
        {
            try
            {
                var log = new AuditLog
                {
                    UserId = userId ?? "Anonymous",
                    UserEmail = (userEmail ?? "Unknown").Length > 256 ? (userEmail ?? "Unknown").Substring(0, 256) : (userEmail ?? "Unknown"),
                    Action = action.Length > 50 ? action.Substring(0, 50) : action,
                    Description = description.Length > 500 ? description.Substring(0, 500) : description,
                    Timestamp = DateTime.UtcNow,
                    IPAddress = GetClientIP(),
                    UserAgent = GetUserAgent(),
                    Success = success,
                    AdditionalInfo = (additionalInfo ?? string.Empty).Length > 1000 ?
                                    (additionalInfo ?? string.Empty).Substring(0, 1000) :
                                    (additionalInfo ?? string.Empty)
                };

                _context.AuditLogs.Add(log);
                await _context.SaveChangesAsync();

                // Sanitize action and description before logging
                var sanitizedAction = SanitizeForLog(action);
                var sanitizedDescription = SanitizeForLog(description);

                _logger.LogInformation("✅ Audit Log Saved: {Action} - {Description}", sanitizedAction, sanitizedDescription);
                Console.WriteLine($"✅ AUDIT LOG SAVED: {sanitizedAction} - {sanitizedDescription}");
            }
            catch (Exception ex)
            {
                // Sanitize action before logging
                var sanitizedAction = SanitizeForLog(action);
                var sanitizedUserEmail = SanitizeForLog(userEmail);

                _logger.LogError(ex, "❌ Error saving audit log for action: {Action}", sanitizedAction);
                Console.WriteLine($"❌ AUDIT LOG ERROR: {ex.Message}");
                Console.WriteLine($"Action: {sanitizedAction}, User: {sanitizedUserEmail}");

                // Try a simplified log without UserAgent
                await TrySimpleLogAsync(userId, userEmail, action, description, success, additionalInfo);
            }
        }

        private async Task TrySimpleLogAsync(string userId, string userEmail, string action, string description, bool success, string additionalInfo)
        {
            try
            {
                var simpleLog = new AuditLog
                {
                    UserId = userId ?? "Anonymous",
                    UserEmail = userEmail ?? "Unknown",
                    Action = action,
                    Description = description,
                    Timestamp = DateTime.UtcNow,
                    IPAddress = "Unknown",
                    UserAgent = "Truncated",
                    Success = success,
                    AdditionalInfo = additionalInfo ?? string.Empty
                };

                _context.AuditLogs.Add(simpleLog);
                await _context.SaveChangesAsync();

                // Sanitize before logging to console
                var sanitizedAction = SanitizeForLog(action);
                Console.WriteLine($"✅ Saved simplified audit log for: {sanitizedAction}");
            }
            catch (Exception ex2)
            {
                var sanitizedMessage = SanitizeForLog(ex2.Message);
                Console.WriteLine($"❌ Even simplified log failed: {sanitizedMessage}");
            }
        }

        public async Task<List<AuditLog>> GetUserLogsAsync(string userId)
        {
            try
            {
                return await _context.AuditLogs
                    .Where(log => log.UserId == userId)
                    .OrderByDescending(log => log.Timestamp)
                    .Take(50)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user logs");
                return new List<AuditLog>();
            }
        }

        public async Task<List<AuditLog>> GetRecentLogsAsync(int count = 100)
        {
            try
            {
                return await _context.AuditLogs
                    .OrderByDescending(log => log.Timestamp)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent logs");
                return new List<AuditLog>();
            }
        }

        public async Task<int> GetFailedLoginCountAsync(string userEmail, TimeSpan timeWindow)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(timeWindow);

                return await _context.AuditLogs
                    .Where(log => log.UserEmail == userEmail
                               && log.Action == "FailedLogin"
                               && log.Timestamp >= cutoffTime)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting failed login count");
                return 0;
            }
        }
    }
}