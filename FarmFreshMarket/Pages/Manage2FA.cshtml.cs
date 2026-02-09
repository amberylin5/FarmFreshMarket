using FarmFreshMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FarmFreshMarket.Pages
{
    [Authorize]
    public class Manage2FAModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ITwoFactorService _twoFactorService;
        private readonly IAuditLogService _auditLogService;

        public bool Is2FAEnabled { get; set; }
        public string TwoFAMethod { get; set; }
        public string UserEmail { get; set; }
        public string PhoneNumber { get; set; }
        public bool IsPhoneVerified { get; set; }
        public DateTime? EnabledDate { get; set; }
        public DateTime? LastUsedDate { get; set; }

        public Manage2FAModel(
            UserManager<IdentityUser> userManager,
            ITwoFactorService twoFactorService,
            IAuditLogService auditLogService)
        {
            _userManager = userManager;
            _twoFactorService = twoFactorService;
            _auditLogService = auditLogService;
        }

        public async Task OnGetAsync()
        {
            await LoadUserSettingsAsync();
        }

        private async Task LoadUserSettingsAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                UserEmail = user.Email;
                Console.WriteLine($"\n?? [LoadUserSettingsAsync] Loading for user: {user.Email}");
                Console.WriteLine($"?? User ID: {user.Id}");

                try
                {
                    // Force fresh database query - bypass any caching
                    var settings = await _twoFactorService.Get2FASettingsAsync(user.Id);

                    // DEBUG: Direct database check
                    Console.WriteLine($"?? Service returned - IsEnabled: {settings.IsEnabled}");

                    // If service says false but we think it should be true, force check
                    if (!settings.IsEnabled && (TempData["ForceCheck"] as string == "true"))
                    {
                        Console.WriteLine($"?? Force checking database directly...");

                        // You might need to add this method to your service or context
                        // For now, let's reload the settings
                        await Task.Delay(100); // Small delay
                        settings = await _twoFactorService.Get2FASettingsAsync(user.Id);
                        Console.WriteLine($"?? After force check - IsEnabled: {settings.IsEnabled}");
                    }

                    Is2FAEnabled = settings.IsEnabled;
                    TwoFAMethod = settings.PreferredMethod;
                    PhoneNumber = settings.PhoneNumber;
                    IsPhoneVerified = settings.IsPhoneVerified;
                    EnabledDate = settings.EnabledAt;
                    LastUsedDate = settings.LastUsed;

                    Console.WriteLine($"?? Model properties set - Is2FAEnabled: {Is2FAEnabled}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"? Error loading settings: {ex.Message}");
                    Console.WriteLine($"? Stack: {ex.StackTrace}");
                }
            }
        }

        // Helper method to format phone number
        public string FormatPhoneNumber(string phoneNumber)
        {
            if (string.IsNullOrEmpty(phoneNumber))
                return phoneNumber;

            if (phoneNumber.StartsWith("65") && phoneNumber.Length == 10)
            {
                return $"+65 {phoneNumber.Substring(2, 4)} {phoneNumber.Substring(6)}";
            }
            else if (phoneNumber.Length == 8 && phoneNumber.All(char.IsDigit))
            {
                return $"+65 {phoneNumber.Substring(0, 4)} {phoneNumber.Substring(4)}";
            }

            return phoneNumber;
        }

        // ======================
        // ENABLE/DISABLE 2FA
        // ======================

        public async Task<IActionResult> OnPostEnableAsync(string method)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            Console.WriteLine($"\n?? [Enable2FA] Attempting to enable 2FA for user: {user.Id}");
            Console.WriteLine($"?? Method selected: {method}");
            Console.WriteLine($"?? User email: {user.Email}");

            try
            {
                // Check current status first
                var currentSettings = await _twoFactorService.Get2FASettingsAsync(user.Id);
                Console.WriteLine($"?? Current settings - IsEnabled: {currentSettings.IsEnabled}, Method: {currentSettings.PreferredMethod}");

                // Enable 2FA
                await _twoFactorService.Enable2FAAsync(user.Id, method);
                Console.WriteLine($"? 2FA enabled in service");

                // Force reload settings from database
                var updatedSettings = await _twoFactorService.Get2FASettingsAsync(user.Id);
                Console.WriteLine($"?? Updated settings - IsEnabled: {updatedSettings.IsEnabled}, Method: {updatedSettings.PreferredMethod}");

                // Clear any cache
                await LoadUserSettingsAsync();

                TempData["SuccessMessage"] = $"Two-factor authentication has been enabled with {method} delivery!";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERROR enabling 2FA: {ex.Message}");
                Console.WriteLine($"? Full error: {ex.ToString()}");
                TempData["ErrorMessage"] = $"Error enabling 2FA: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDisableAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            try
            {
                await _twoFactorService.Disable2FAAsync(user.Id);
                TempData["SuccessMessage"] = "Two-factor authentication has been disabled.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error disabling 2FA: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostChangeMethodAsync(string method)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            try
            {
                if ((method == "SMS" || method == "Both"))
                {
                    var settings = await _twoFactorService.Get2FASettingsAsync(user.Id);
                    if (!settings.IsPhoneVerified)
                    {
                        TempData["ErrorMessage"] = "Please verify your phone number first for SMS delivery.";
                        return RedirectToPage();
                    }
                }

                await _twoFactorService.Enable2FAAsync(user.Id, method);
                TempData["SuccessMessage"] = $"Delivery method changed to {method}.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error changing method: {ex.Message}";
            }

            return RedirectToPage();
        }

        // ======================
        // PHONE NUMBER METHODS
        // ======================

        public async Task<IActionResult> OnPostAddPhoneAsync(string phoneNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            try
            {
                // Format the phone number if it's Singapore format
                if (!string.IsNullOrEmpty(phoneNumber))
                {
                    phoneNumber = phoneNumber.Trim();

                    // If it's a Singapore number without country code, add it
                    if (phoneNumber.Length == 8 && phoneNumber.All(char.IsDigit))
                    {
                        phoneNumber = "65" + phoneNumber;
                    }
                }

                var success = await _twoFactorService.UpdatePhoneNumberAsync(user.Id, phoneNumber);

                if (success)
                {
                    TempData["SuccessMessage"] = "Verification code sent to your phone.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Please enter a valid phone number (minimum 8 digits).";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostVerifyPhoneAsync(string code)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            try
            {
                var success = await _twoFactorService.VerifyPhoneNumberAsync(user.Id, code);
                if (success)
                {
                    TempData["SuccessMessage"] = "Phone number verified successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Invalid verification code.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostResendCodeAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            try
            {
                await _twoFactorService.GenerateAndSend2FACodeAsync(user.Id, "VerifyPhone", "SMS");
                TempData["SuccessMessage"] = "New verification code sent to your phone.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdatePhoneAsync(string newPhoneNumber)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            try
            {
                // Format the phone number if it's Singapore format
                if (!string.IsNullOrEmpty(newPhoneNumber))
                {
                    newPhoneNumber = newPhoneNumber.Trim();

                    // If it's a Singapore number without country code, add it
                    if (newPhoneNumber.Length == 8 && newPhoneNumber.All(char.IsDigit))
                    {
                        newPhoneNumber = "65" + newPhoneNumber;
                    }
                }

                var success = await _twoFactorService.UpdatePhoneNumberAsync(user.Id, newPhoneNumber);
                if (success)
                {
                    TempData["SuccessMessage"] = "New verification code sent to your updated phone.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Please enter a valid phone number (minimum 8 digits).";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostTestSmsAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToPage("/Login");

            try
            {
                var settings = await _twoFactorService.Get2FASettingsAsync(user.Id);
                if (!string.IsNullOrEmpty(settings.PhoneNumber))
                {
                    await _twoFactorService.SendTestSmsAsync(settings.PhoneNumber);
                    TempData["SuccessMessage"] = "Test SMS sent to your phone.";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            return RedirectToPage();
        }
    }
}