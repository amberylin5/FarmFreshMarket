using FarmFreshMarket.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace FarmFreshMarket.Services
{
    public interface ISessionManagerService
    {
        Task<string> CreateSessionAsync(string userId, string ipAddress, string userAgent);
        Task<bool> ValidateSessionAsync(string userId, string sessionToken);
        Task<List<UserSession>> GetActiveSessionsAsync(string userId);
        Task<bool> TerminateSessionAsync(string sessionToken);
        Task<bool> TerminateAllOtherSessionsAsync(string userId, string currentSessionToken);
        Task UpdateLastActivityAsync(string sessionToken);
        Task CleanupExpiredSessionsAsync(TimeSpan maxIdleTime);
        Task<int> GetActiveSessionCountAsync(string userId);
    }

    public class SessionManagerService : ISessionManagerService
    {
        private readonly AuthDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SessionManagerService> _logger;

        public SessionManagerService(AuthDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<SessionManagerService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        private string GenerateSessionToken()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] tokenData = new byte[32];
            rng.GetBytes(tokenData);
            return Convert.ToBase64String(tokenData);
        }

        private string GetDeviceInfo(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
                return "Unknown";

            userAgent = userAgent.ToLower();

            if (userAgent.Contains("mobile") || userAgent.Contains("android") || userAgent.Contains("iphone"))
                return "Mobile";
            if (userAgent.Contains("tablet") || userAgent.Contains("ipad"))
                return "Tablet";
            if (userAgent.Contains("windows"))
                return "Windows PC";
            if (userAgent.Contains("mac os"))
                return "Mac";
            if (userAgent.Contains("linux"))
                return "Linux";

            return "Desktop";
        }

        public async Task<string> CreateSessionAsync(string userId, string ipAddress, string userAgent)
        {
            try
            {
                var sessionToken = GenerateSessionToken();

                var session = new UserSession
                {
                    UserId = userId,
                    SessionToken = sessionToken,
                    LoginTime = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow,
                    IPAddress = ipAddress,
                    UserAgent = userAgent.Length > 500 ? userAgent.Substring(0, 500) : userAgent,
                    DeviceInfo = GetDeviceInfo(userAgent),
                    IsActive = true
                };

                _context.UserSessions.Add(session);
                await _context.SaveChangesAsync();

                return sessionToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating session for user {userId}");
                return null!;
            }
        }

        public async Task<bool> ValidateSessionAsync(string userId, string sessionToken)
        {
            try
            {
                var session = await _context.UserSessions
                    .FirstOrDefaultAsync(s => s.UserId == userId && s.SessionToken == sessionToken && s.IsActive);

                if (session == null)
                    return false;

                // Check for 5 minutes of inactivity (changed from 30)
                if (session.LastActivity.HasValue && (DateTime.UtcNow - session.LastActivity.Value).TotalMinutes > 5)
                {
                    session.IsActive = false;
                    session.LogoutTime = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return false;
                }

                // Update last activity
                session.LastActivity = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating session for user {UserId}", userId);
                return false;
            }
        }

        public async Task<List<UserSession>> GetActiveSessionsAsync(string userId)
        {
            try
            {
                return await _context.UserSessions
                    .Where(s => s.UserId == userId && s.IsActive)
                    .OrderByDescending(s => s.LoginTime)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting active sessions for user {userId}");
                return new List<UserSession>();
            }
        }

        public async Task<bool> TerminateSessionAsync(string sessionToken)
        {
            try
            {
                var session = await _context.UserSessions
                    .FirstOrDefaultAsync(s => s.SessionToken == sessionToken && s.IsActive);

                if (session == null)
                    return false;

                session.IsActive = false;
                session.LogoutTime = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating session {SessionToken}", sessionToken);
                return false;
            }
        }

        public async Task<bool> TerminateAllOtherSessionsAsync(string userId, string currentSessionToken)
        {
            try
            {
                var otherSessions = await _context.UserSessions
                    .Where(s => s.UserId == userId && s.SessionToken != currentSessionToken && s.IsActive)
                    .ToListAsync();

                foreach (var session in otherSessions)
                {
                    session.IsActive = false;
                    session.LogoutTime = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error terminating other sessions for user {userId}");
                return false;
            }
        }

        public async Task UpdateLastActivityAsync(string sessionToken)
        {
            try
            {
                var session = await _context.UserSessions
                    .FirstOrDefaultAsync(s => s.SessionToken == sessionToken && s.IsActive);

                if (session != null)
                {
                    session.LastActivity = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating last activity for session {sessionToken}");
            }
        }

        public async Task CleanupExpiredSessionsAsync(TimeSpan maxIdleTime)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow.Subtract(maxIdleTime);

                var expiredSessions = await _context.UserSessions
                    .Where(s => s.IsActive && s.LastActivity < cutoffTime)
                    .ToListAsync();

                foreach (var session in expiredSessions)
                {
                    session.IsActive = false;
                    session.LogoutTime = DateTime.UtcNow;
                }

                if (expiredSessions.Any())
                {
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up expired sessions");
            }
        }

        public async Task<int> GetActiveSessionCountAsync(string userId)
        {
            try
            {
                return await _context.UserSessions
                    .CountAsync(s => s.UserId == userId && s.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting active session count for user {userId}");
                return 0;
            }
        }
    }
}