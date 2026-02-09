using FarmFreshMarket.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FarmFreshMarket.Services
{
    public class SmsService : ISmsService
    {
        private readonly ILogger<SmsService> _logger;
        private readonly IOptions<SmsSettings> _smsSettings;

        public SmsService(ILogger<SmsService> logger, IOptions<SmsSettings> smsSettings = null)
        {
            _logger = logger;
            _smsSettings = smsSettings;
        }

        public async Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            try
            {
                // Always simulate for assignment
                Console.WriteLine($"\n📱 SMS SIMULATION:");
                Console.WriteLine($"To: {MaskPhoneNumber(phoneNumber)}");
                Console.WriteLine($"Message: {message}");
                Console.WriteLine($"Time: {DateTime.Now:HH:mm:ss}");
                Console.WriteLine($"Length: {message.Length} characters");
                Console.WriteLine();

                _logger.LogInformation("SMS simulated to {PhoneNumber}: {Message}",
                    MaskPhoneNumber(phoneNumber), message);

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error simulating SMS");
                return false;
            }
        }

        public async Task<bool> Send2FACodeAsync(string phoneNumber, string code, string purpose)
        {
            var message = purpose switch
            {
                "Login" => $"Your Fresh Farm Market login code: {code}. Valid for 10 minutes.",
                "VerifyPhone" => $"Your Fresh Farm Market verification code: {code}. Valid for 10 minutes.",
                _ => $"Your Fresh Farm Market code: {code}. Valid for 10 minutes."
            };

            return await SendSmsAsync(phoneNumber, message);
        }

        private string MaskPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length < 4)
                return "***-***-****";

            return $"***-***-{phoneNumber.Substring(phoneNumber.Length - 4)}";
        }
    }
}