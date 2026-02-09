using FarmFreshMarket.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FarmFreshMarket.Pages
{
    public class Verify2FAModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ITwoFactorService _twoFactorService;
        private readonly IAuditLogService _auditLogService;
        private readonly ISessionManagerService _sessionManager;

        [BindProperty]
        public string Code { get; set; }

        [BindProperty(SupportsGet = true)]
        public string UserId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string Purpose { get; set; } = "Login";

        public string UserEmail { get; set; }
        public string DeliveryMethod { get; set; } = "Both";

        public Verify2FAModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ITwoFactorService twoFactorService,
            IAuditLogService auditLogService,
            ISessionManagerService sessionManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _twoFactorService = twoFactorService;
            _auditLogService = auditLogService;
            _sessionManager = sessionManager;
        }

        public async Task OnGetAsync()
        {
            if (!string.IsNullOrEmpty(UserId))
            {
                var user = await _userManager.FindByIdAsync(UserId);
                if (user != null)
                {
                    UserEmail = user.Email;
                    var settings = await _twoFactorService.Get2FASettingsAsync(UserId);
                    DeliveryMethod = settings.PreferredMethod;
                }
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(UserId) || string.IsNullOrEmpty(Code))
            {
                ModelState.AddModelError(string.Empty, "Verification code is required.");
                return Page();
            }

            var (isValid, message) = await _twoFactorService.Verify2FACodeAsync(UserId, Code, Purpose);

            if (!isValid)
            {
                ModelState.AddModelError(string.Empty, message);
                return await LoadPageDataAsync();
            }

            // Code is valid - complete the process
            var user = await _userManager.FindByIdAsync(UserId);
            if (user != null)
            {
                if (Purpose == "Login")
                {
                    await CompleteLoginAsync(user);
                    return RedirectToPage("Index");
                }
                else if (Purpose == "Enable2FA")
                {
                    TempData["SuccessMessage"] = "Two-factor authentication enabled successfully!";
                    return RedirectToPage("Manage2FA");
                }
                else if (Purpose == "VerifyPhone")
                {
                    TempData["SuccessMessage"] = "Phone number verified successfully!";
                    return RedirectToPage("Manage2FA");
                }
            }

            ModelState.AddModelError(string.Empty, "User not found.");
            return await LoadPageDataAsync();
        }

        public async Task<IActionResult> OnPostResendAsync()
        {
            if (!string.IsNullOrEmpty(UserId))
            {
                try
                {
                    var user = await _userManager.FindByIdAsync(UserId);
                    if (user != null)
                    {
                        var settings = await _twoFactorService.Get2FASettingsAsync(UserId);
                        await _twoFactorService.GenerateAndSend2FACodeAsync(UserId, Purpose, settings.PreferredMethod);

                        TempData["ResendMessage"] = "New verification code sent!";
                        return RedirectToPage(new { UserId, Purpose });
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"Error resending code: {ex.Message}");
                }
            }

            return await LoadPageDataAsync();
        }

        private async Task<IActionResult> LoadPageDataAsync()
        {
            if (!string.IsNullOrEmpty(UserId))
            {
                var user = await _userManager.FindByIdAsync(UserId);
                if (user != null)
                {
                    UserEmail = user.Email;
                    var settings = await _twoFactorService.Get2FASettingsAsync(UserId);
                    DeliveryMethod = settings.PreferredMethod;
                }
            }
            return Page();
        }

        private async Task CompleteLoginAsync(IdentityUser user)
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            await _sessionManager.CreateSessionAsync(user.Id, ipAddress, userAgent);
            await _auditLogService.LogAsync(user.Id, user.Email, "Login", "User logged in with 2FA");

            // Sign in the user
            await _signInManager.SignInAsync(user, isPersistent: false);
        }
    }
}