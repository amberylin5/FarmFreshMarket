using FarmFreshMarket.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Linq;

namespace FarmFreshMarket.Services
{
    public class TwoFactorService : ITwoFactorService
    {
        private readonly AuthDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<TwoFactorService> _logger;
        private readonly UserManager<IdentityUser> _userManager;

        public TwoFactorService(
            AuthDbContext context,
            IEmailService emailService,
            ISmsService smsService,
            IHttpContextAccessor httpContextAccessor,
            ILogger<TwoFactorService> logger,
            UserManager<IdentityUser> userManager)
        {
            _context = context;
            _emailService = emailService;
            _smsService = smsService;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _userManager = userManager;
        }

        private string Generate6DigitCode()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] randomNumber = new byte[4];
            rng.GetBytes(randomNumber);
            int value = BitConverter.ToInt32(randomNumber, 0) & 0x7FFFFFFF;
            return (value % 1000000).ToString("D6");
        }

        public async Task<string> GenerateAndSend2FACodeAsync(string userId, string purpose, string method = null, string phoneNumber = null)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null) throw new InvalidOperationException("User not found");

                var settings = await GetOrCreate2FASettingsAsync(userId);
                var deliveryMethod = method ?? settings.PreferredMethod;

                // Use provided phone number or get from settings
                var phoneToUse = phoneNumber ?? settings.PhoneNumber;
                var isPhoneVerified = phoneNumber == null ? settings.IsPhoneVerified : false;

                var code = Generate6DigitCode();
                var expiresAt = DateTime.UtcNow.AddMinutes(10);

                await InvalidateExistingCodesAsync(userId, purpose);

                var twoFactorCode = new TwoFactorCode
                {
                    UserId = userId,
                    Code = code,
                    Purpose = purpose,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = expiresAt,
                    IPAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                    UserAgent = _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].ToString() ?? "Unknown",
                    DeliveryMethod = deliveryMethod
                };

                _context.TwoFactorCodes.Add(twoFactorCode);
                await _context.SaveChangesAsync();

                await Send2FAEmailAndSmsAsync(user.Email, phoneToUse, code, purpose, deliveryMethod, isPhoneVerified);

                return code;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating 2FA code");
                throw;
            }
        }

        private async Task<User2FASettings> GetOrCreate2FASettingsAsync(string userId)
        {
            try
            {
                Console.WriteLine($"🔍 [GetOrCreate2FASettingsAsync] Called for user: {userId}");

                // Get tracked entity from database
                var settings = await _context.User2FASettings
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (settings == null)
                {
                    Console.WriteLine($"🔍 Creating new settings for user: {userId}");
                    // Create new settings
                    var user = await _userManager.FindByIdAsync(userId);

                    settings = new User2FASettings
                    {
                        UserId = userId,
                        IsEnabled = false,
                        PreferredMethod = "Email",
                        Email = user?.Email ?? "",
                        PhoneNumber = "",
                        IsPhoneVerified = false,
                        LastUsed = DateTime.UtcNow,
                        FailedAttempts = 0
                    };

                    _context.User2FASettings.Add(settings);

                    try
                    {
                        await _context.SaveChangesAsync();
                        Console.WriteLine($"✅ New settings created and saved");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"❌ Error saving new settings: {ex.Message}");
                        throw;
                    }
                }
                else
                {
                    Console.WriteLine($"🔍 Found existing settings - IsEnabled: {settings.IsEnabled}, Method: {settings.PreferredMethod}");
                }

                return settings;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR in GetOrCreate2FASettingsAsync: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");

                _logger.LogError(ex, "Error getting/creating 2FA settings for user {UserId}", userId);

                // Return safe defaults
                return new User2FASettings
                {
                    UserId = userId,
                    IsEnabled = false,
                    PreferredMethod = "Email",
                    Email = "",
                    PhoneNumber = "",
                    IsPhoneVerified = false,
                    LastUsed = DateTime.UtcNow,
                    FailedAttempts = 0
                };
            }
        }

        private async Task Send2FAEmailAndSmsAsync(string email, string phoneNumber, string code, string purpose, string method, bool isPhoneVerified)
        {
            // Always send email for assignment
            await SendEmailCodeAsync(email, code, purpose);

            bool shouldSendSms = (method == "SMS" || method == "Both") &&
                                 !string.IsNullOrEmpty(phoneNumber) &&
                                 (isPhoneVerified || purpose == "VerifyPhone");

            if (shouldSendSms)
            {
                await SendSmsCodeAsync(phoneNumber, code, purpose);
            }
        }

        private async Task SendEmailCodeAsync(string email, string code, string purpose)
        {
            var subject = "Your 2FA Code - Fresh Farm Market";
            var body = $@"
            <h2>Two-Factor Authentication Code</h2>
            <p>Your verification code is: <strong>{code}</strong></p>
            <p>Purpose: {purpose}</p>
            <p>Expires in: 10 minutes</p>
            <p>If you didn't request this, please ignore this email.</p>";

            await _emailService.SendEmailAsync(email, subject, body);
        }

        private async Task SendSmsCodeAsync(string phoneNumber, string code, string purpose)
        {
            var message = $"Your Fresh Farm Market code: {code}. Valid for 10 minutes.";
            await _smsService.SendSmsAsync(phoneNumber, message);
        }

        private string MaskPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length < 4)
                return "***-***-****";

            return $"***-***-{phoneNumber.Substring(phoneNumber.Length - 4)}";
        }

        private async Task InvalidateExistingCodesAsync(string userId, string purpose)
        {
            var existingCodes = await _context.TwoFactorCodes
                .Where(c => c.UserId == userId && c.Purpose == purpose && !c.IsUsed && c.ExpiresAt > DateTime.UtcNow)
                .ToListAsync();

            foreach (var code in existingCodes)
            {
                code.IsUsed = true;
                code.UsedAt = DateTime.UtcNow;
            }
        }

        public async Task<(bool isValid, string message)> Verify2FACodeAsync(string userId, string code, string purpose)
        {
            Console.WriteLine($"\n🔐 [TwoFactorService.Verify2FACodeAsync]");
            Console.WriteLine($"🔐 UserId: {userId}");
            Console.WriteLine($"🔐 Code entered: {code}");
            Console.WriteLine($"🔐 Purpose: {purpose}");

            try
            {
                code = code?.Replace(" ", "") ?? "";

                if (string.IsNullOrEmpty(code) || code.Length != 6 || !code.All(char.IsDigit))
                {
                    Console.WriteLine($"❌ Invalid code format");
                    return (false, "Invalid code. Must be 6 digits.");
                }

                var twoFactorCode = await _context.TwoFactorCodes
                    .Where(c => c.UserId == userId
                             && c.Code == code
                             && c.Purpose == purpose
                             && !c.IsUsed
                             && c.ExpiresAt > DateTime.UtcNow)
                    .FirstOrDefaultAsync();

                if (twoFactorCode == null)
                {
                    Console.WriteLine($"❌ Code not found or expired");
                    return (false, "Invalid or expired code.");
                }

                // Mark as used
                twoFactorCode.IsUsed = true;
                twoFactorCode.UsedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                Console.WriteLine($"✅ Code verified successfully!");
                return (true, "Verification successful!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error verifying code: {ex.Message}");
                _logger.LogError(ex, "Error verifying 2FA code");
                return (false, "An error occurred during verification.");
            }
        }

        public async Task<bool> Is2FAEnabledAsync(string userId)
        {
            Console.WriteLine($"🔍 [Is2FAEnabledAsync] Checking for user: {userId}");

            try
            {
                // Get directly from database, not through GetOrCreate
                var settings = await _context.User2FASettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (settings == null)
                {
                    Console.WriteLine($"🔍 No settings found, 2FA is disabled");
                    return false;
                }

                Console.WriteLine($"🔍 Found settings - IsEnabled: {settings.IsEnabled}");
                return settings.IsEnabled;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error checking 2FA status: {ex.Message}");
                return false;
            }
        }

        public async Task Enable2FAAsync(string userId, string method = "Email")
        {
            Console.WriteLine($"\n🔄 [TwoFactorService.Enable2FAAsync] START");
            Console.WriteLine($"🔄 UserId: {userId}");
            Console.WriteLine($"🔄 Method: {method}");

            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    Console.WriteLine($"❌ User not found: {userId}");
                    throw new InvalidOperationException("User not found");
                }

                // Get tracked entity or create new one
                var settings = await _context.User2FASettings
                    .FirstOrDefaultAsync(s => s.UserId == userId);

                if (settings == null)
                {
                    Console.WriteLine($"🔄 Creating NEW settings");
                    settings = new User2FASettings
                    {
                        UserId = userId,
                        IsEnabled = true,
                        PreferredMethod = method,
                        EnabledAt = DateTime.UtcNow,
                        Email = user.Email,
                        PhoneNumber = "",
                        IsPhoneVerified = false,
                        LastUsed = DateTime.UtcNow,
                        FailedAttempts = 0
                    };
                    _context.User2FASettings.Add(settings);
                }
                else
                {
                    Console.WriteLine($"🔄 Updating EXISTING settings ID: {settings.Id}");
                    Console.WriteLine($"🔄 Before - IsEnabled: {settings.IsEnabled}, Method: {settings.PreferredMethod}");

                    settings.IsEnabled = true;
                    settings.PreferredMethod = method;
                    settings.EnabledAt = DateTime.UtcNow;
                    settings.Email = user.Email;

                    Console.WriteLine($"🔄 After - IsEnabled: {settings.IsEnabled}, Method: {settings.PreferredMethod}");
                }

                // Save to database
                var changes = await _context.SaveChangesAsync();
                Console.WriteLine($"✅ Database saved. Changes: {changes}");

                // Verify save
                var verify = await _context.User2FASettings
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == userId);
                Console.WriteLine($"✅ Verification - IsEnabled: {verify?.IsEnabled}, Method: {verify?.PreferredMethod}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR in Enable2FAAsync: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        public async Task Disable2FAAsync(string userId)
        {
            var settings = await GetOrCreate2FASettingsAsync(userId);
            settings.IsEnabled = false;
            await _context.SaveChangesAsync();
        }

        public async Task<User2FASettings> Get2FASettingsAsync(string userId)
        {
            Console.WriteLine($"\n🔍 [Get2FASettingsAsync] Called for user: {userId}");

            try
            {
                // Use AsNoTracking to prevent EF caching
                var settings = await _context.User2FASettings
                    .AsNoTracking() // CRITICAL: Prevents caching
                    .Where(s => s.UserId == userId)
                    .FirstOrDefaultAsync();

                if (settings != null)
                {
                    Console.WriteLine($"✅ Found in database - ID: {settings.Id}, IsEnabled: {settings.IsEnabled}");
                    return new User2FASettings
                    {
                        Id = settings.Id,
                        UserId = settings.UserId,
                        IsEnabled = settings.IsEnabled,
                        EnabledAt = settings.EnabledAt,
                        PreferredMethod = settings.PreferredMethod ?? "Email",
                        Email = settings.Email ?? "",
                        PhoneNumber = settings.PhoneNumber ?? "",
                        IsPhoneVerified = settings.IsPhoneVerified,
                        LastUsed = settings.LastUsed == default ? DateTime.UtcNow : settings.LastUsed,
                        FailedAttempts = settings.FailedAttempts,
                        LockoutUntil = settings.LockoutUntil
                    };
                }

                Console.WriteLine($"❌ No settings found in database");
                return new User2FASettings
                {
                    UserId = userId,
                    IsEnabled = false,
                    PreferredMethod = "Email",
                    Email = "",
                    PhoneNumber = "",
                    IsPhoneVerified = false,
                    LastUsed = DateTime.UtcNow,
                    FailedAttempts = 0
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ ERROR in Get2FASettingsAsync: {ex.Message}");
                return new User2FASettings
                {
                    UserId = userId,
                    IsEnabled = false,
                    PreferredMethod = "Email",
                    Email = "",
                    PhoneNumber = "",
                    IsPhoneVerified = false,
                    LastUsed = DateTime.UtcNow,
                    FailedAttempts = 0
                };
            }
        }

        public async Task<bool> UpdatePhoneNumberAsync(string userId, string phoneNumber)
        {
            try
            {
                // Validate phone number
                if (string.IsNullOrWhiteSpace(phoneNumber))
                {
                    return false;
                }

                // Clean the phone number (remove spaces, dashes, etc.)
                phoneNumber = new string(phoneNumber.Where(char.IsDigit).ToArray());

                // Singapore phone number validation
                if (phoneNumber.StartsWith("65"))
                {
                    // This is +65XXXXXXXX format
                    // After "65" prefix, we need 8 digits
                    if (phoneNumber.Length != 10) // 65 + 8 digits = 10
                    {
                        return false;
                    }
                }
                else if (phoneNumber.Length == 8)
                {
                    // Local Singapore number (8 digits)
                    // Add the country code
                    phoneNumber = "65" + phoneNumber;
                }
                else if (phoneNumber.Length < 8)
                {
                    return false;
                }

                var settings = await GetOrCreate2FASettingsAsync(userId);
                settings.PhoneNumber = phoneNumber;
                settings.IsPhoneVerified = false;

                // Save changes
                await _context.SaveChangesAsync();

                // Send verification code
                await GenerateAndSend2FACodeAsync(userId, "VerifyPhone", "SMS", phoneNumber);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating phone number for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> VerifyPhoneNumberAsync(string userId, string code)
        {
            var (isValid, message) = await Verify2FACodeAsync(userId, code, "VerifyPhone");

            if (isValid)
            {
                var settings = await GetOrCreate2FASettingsAsync(userId);
                settings.IsPhoneVerified = true;
                await _context.SaveChangesAsync();
            }

            return isValid;
        }

        public async Task<bool> SendTestSmsAsync(string phoneNumber)
        {
            try
            {
                var testCode = "123456";
                await SendSmsCodeAsync(phoneNumber, testCode, "Test");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending test SMS");
                return false;
            }
        }
    }
}