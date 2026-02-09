using FarmFreshMarket.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FarmFreshMarket.Services
{
    public class PasswordService : IPasswordService
    {
        private readonly AuthDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<PasswordService> _logger;

        public PasswordService(AuthDbContext context, UserManager<IdentityUser> userManager, ILogger<PasswordService> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<bool> CanChangePasswordAsync(string userId)
        {
            try
            {
                var lastChange = await GetLastPasswordChangeDateAsync(userId);

                if (!lastChange.HasValue)
                    return true; // First time changing password

                // Minimum password age: 2 minutes (for testing/demo)
                var minAge = TimeSpan.FromMinutes(2);
                var timeSinceLastChange = DateTime.UtcNow - lastChange.Value;

                return timeSinceLastChange >= minAge;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking password change eligibility for user {userId}");
                return false;
            }
        }

        public async Task<(bool canChange, string errorMessage)> CheckPasswordChangeEligibilityAsync(string userId)
        {
            try
            {
                var lastChange = await GetLastPasswordChangeDateAsync(userId);

                if (!lastChange.HasValue)
                    return (true, "OK");

                var timeSinceLastChange = DateTime.UtcNow - lastChange.Value;

                // Check minimum age (2 minutes)
                var minAge = TimeSpan.FromMinutes(2);
                if (timeSinceLastChange < minAge)
                {
                    var minutesLeft = (minAge - timeSinceLastChange).TotalMinutes;
                    return (false, $"You cannot change your password yet. Please wait {Math.Ceiling(minutesLeft)} more minutes.");
                }

                // Check maximum age (5 minutes) - warning only
                var maxAge = TimeSpan.FromMinutes(5);
                if (timeSinceLastChange > maxAge)
                {
                    return (true, "WARNING: Your password is over 5 minutes old. Please change it for security.");
                }

                return (true, "OK");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking password change eligibility for user {userId}");
                return (false, "Error checking password eligibility.");
            }
        }

        public async Task<bool> IsPasswordInHistoryAsync(string userId, string newPassword)
        {
            try
            {
                // Get last 2 passwords from history
                var recentPasswords = await _context.PasswordHistories
                    .Where(ph => ph.UserId == userId)
                    .OrderByDescending(ph => ph.ChangedDate)
                    .Take(2)
                    .ToListAsync();

                if (!recentPasswords.Any())
                    return false;

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return false;

                // Check if new password matches any of the recent passwords
                foreach (var history in recentPasswords)
                {
                    var result = _userManager.PasswordHasher.VerifyHashedPassword(user, history.PasswordHash, newPassword);
                    if (result == PasswordVerificationResult.Success)
                        return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking password history for user {userId}");
                return false;
            }
        }

        public async Task AddToPasswordHistoryAsync(string userId, string passwordHash, string changedBy = "User")
        {
            try
            {
                // Mark all existing passwords as not current
                var existingPasswords = await _context.PasswordHistories
                    .Where(ph => ph.UserId == userId && ph.IsCurrent)
                    .ToListAsync();

                foreach (var password in existingPasswords)
                {
                    password.IsCurrent = false;
                }

                // Add new password to history
                var passwordHistory = new PasswordHistory
                {
                    UserId = userId,
                    PasswordHash = passwordHash,
                    ChangedDate = DateTime.UtcNow,
                    ChangedBy = changedBy,
                    IsCurrent = true
                };

                _context.PasswordHistories.Add(passwordHistory);

                // Keep only last 5 passwords (for history tracking)
                var allPasswords = await _context.PasswordHistories
                    .Where(ph => ph.UserId == userId)
                    .OrderByDescending(ph => ph.ChangedDate)
                    .ToListAsync();

                if (allPasswords.Count > 5)
                {
                    var toRemove = allPasswords.Skip(5);
                    _context.PasswordHistories.RemoveRange(toRemove);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error adding password to history for user {userId}");
            }
        }

        public async Task<List<PasswordHistory>> GetPasswordHistoryAsync(string userId, int count = 5)
        {
            try
            {
                return await _context.PasswordHistories
                    .Where(ph => ph.UserId == userId)
                    .OrderByDescending(ph => ph.ChangedDate)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting password history for user {userId}");
                return new List<PasswordHistory>();
            }
        }

        public async Task<int> GetPasswordAgeInMinutesAsync(string userId)
        {
            try
            {
                var lastChange = await GetLastPasswordChangeDateAsync(userId);

                if (!lastChange.HasValue)
                    return 0;

                return (int)(DateTime.UtcNow - lastChange.Value).TotalMinutes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting password age for user {userId}");
                return 0;
            }
        }

        public async Task<DateTime?> GetLastPasswordChangeDateAsync(string userId)
        {
            try
            {
                var currentPassword = await _context.PasswordHistories
                    .Where(ph => ph.UserId == userId && ph.IsCurrent)
                    .OrderByDescending(ph => ph.ChangedDate)
                    .FirstOrDefaultAsync();

                return currentPassword?.ChangedDate;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting last password change date for user {userId}");
                return null;
            }
        }
        public async Task<bool> IsPasswordExpiredDueToInactivityAsync(string userId, int maxInactiveMinutes = 5)
        {
            try
            {
                var member = await _context.Members.FirstOrDefaultAsync(m => m.UserId == userId);
                if (member == null)
                    return false;

                var timeSinceLastLogin = DateTime.UtcNow - member.LastLoginTime;
                return timeSinceLastLogin.TotalMinutes > maxInactiveMinutes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking password expiry for inactivity for user {userId}");
                return false;
            }
        }

        public async Task UpdateLastLoginTimeAsync(string userId)
        {
            try
            {
                var member = await _context.Members.FirstOrDefaultAsync(m => m.UserId == userId);
                if (member != null)
                {
                    member.LastLoginTime = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating last login time for user {userId}");
            }
        }

        public Task<bool> ValidatePasswordComplexityAsync(string password)
        {
            try
            {
                if (string.IsNullOrEmpty(password))
                    return Task.FromResult(false);

                // Check length
                if (password.Length < 12)
                    return Task.FromResult(false);

                // Check for uppercase
                if (!password.Any(char.IsUpper))
                    return Task.FromResult(false);

                // Check for lowercase
                if (!password.Any(char.IsLower))
                    return Task.FromResult(false);

                // Check for digit
                if (!password.Any(char.IsDigit))
                    return Task.FromResult(false);

                // Check for special character
                if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                    return Task.FromResult(false);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating password complexity");
                return Task.FromResult(false);
            }
        }
    }
}