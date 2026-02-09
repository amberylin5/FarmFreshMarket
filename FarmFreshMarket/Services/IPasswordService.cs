using FarmFreshMarket.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FarmFreshMarket.Services
{
    public interface IPasswordService
    {
        Task<bool> CanChangePasswordAsync(string userId);
        Task<(bool canChange, string errorMessage)> CheckPasswordChangeEligibilityAsync(string userId);
        Task<bool> IsPasswordInHistoryAsync(string userId, string newPassword);
        Task AddToPasswordHistoryAsync(string userId, string passwordHash, string changedBy = "User");
        Task<List<PasswordHistory>> GetPasswordHistoryAsync(string userId, int count = 5);
        Task<int> GetPasswordAgeInMinutesAsync(string userId);
        Task<DateTime?> GetLastPasswordChangeDateAsync(string userId);
        Task<bool> ValidatePasswordComplexityAsync(string password);

        // ADD THESE METHODS:
        Task<bool> IsPasswordExpiredDueToInactivityAsync(string userId, int maxInactiveMinutes = 5);
        Task UpdateLastLoginTimeAsync(string userId);
    }
}