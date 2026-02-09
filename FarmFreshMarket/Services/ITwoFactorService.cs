using FarmFreshMarket.Models;

namespace FarmFreshMarket.Services
{
    public interface ITwoFactorService
    {
        Task<string> GenerateAndSend2FACodeAsync(string userId, string purpose, string method = null, string phoneNumber = null);
        Task<(bool isValid, string message)> Verify2FACodeAsync(string userId, string code, string purpose);
        Task<bool> Is2FAEnabledAsync(string userId);
        Task Enable2FAAsync(string userId, string method = "Email");
        Task Disable2FAAsync(string userId);
        Task<User2FASettings> Get2FASettingsAsync(string userId);
        Task<bool> UpdatePhoneNumberAsync(string userId, string phoneNumber);
        Task<bool> VerifyPhoneNumberAsync(string userId, string code);
        Task<bool> SendTestSmsAsync(string phoneNumber);
    }
}