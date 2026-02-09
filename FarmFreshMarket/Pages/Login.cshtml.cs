using FarmFreshMarket.Models;
using FarmFreshMarket.Services;
using FarmFreshMarket.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FarmFreshMarket.Pages
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IAuditLogService _auditLogService;
        private readonly ISessionManagerService _sessionManager;
        private readonly ITwoFactorService _twoFactorService;

        [BindProperty]
        public Login LoginData { get; set; } = new Login();

        public string TwoFactorCode { get; set; }

        public bool RequiresTwoFactor { get; set; } = false;
        public string TwoFactorEmail { get; set; }
        public string TwoFactorUserId { get; set; }

        public LoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            IAuditLogService auditLogService,
            ISessionManagerService sessionManager,
            ITwoFactorService twoFactorService)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _auditLogService = auditLogService;
            _sessionManager = sessionManager;
            _twoFactorService = twoFactorService;
        }

        public IActionResult OnGet(bool reset = false, string userId = null)
        {
            Console.WriteLine($"\n?? [LoginModel.OnGet] Loading login page");
            Console.WriteLine($"?? Reset: {reset}, UserId param: {userId}");

            // Force reset if requested
            if (reset)
            {
                ClearAllState();
                return Page();
            }

            // Check if we have 2FA state in Session
            var sessionUserId = HttpContext.Session.GetString("TwoFactorUserId");
            var sessionEmail = HttpContext.Session.GetString("TwoFactorEmail");
            var sessionRequires2FA = HttpContext.Session.GetString("RequiresTwoFactor");

            Console.WriteLine($"?? Session - UserId: {sessionUserId}, Email: {sessionEmail}, Requires2FA: {sessionRequires2FA}");

            // Check if we should show 2FA form
            if (sessionRequires2FA == "true" && !string.IsNullOrEmpty(sessionUserId))
            {
                Console.WriteLine($"?? Showing 2FA form from Session");

                RequiresTwoFactor = true;
                TwoFactorEmail = sessionEmail;
                TwoFactorUserId = sessionUserId;

                return Page();
            }

            // Also check query parameter for userId (as fallback)
            if (!string.IsNullOrEmpty(userId))
            {
                Console.WriteLine($"?? Showing 2FA form from query parameter");

                var user = _userManager.FindByIdAsync(userId).Result;
                if (user != null)
                {
                    RequiresTwoFactor = true;
                    TwoFactorUserId = userId;
                    TwoFactorEmail = user.Email;

                    // Store in Session for next requests
                    HttpContext.Session.SetString("TwoFactorUserId", userId);
                    HttpContext.Session.SetString("TwoFactorEmail", user.Email);
                    HttpContext.Session.SetString("RequiresTwoFactor", "true");

                    return Page();
                }
            }

            // Default: show regular login
            Console.WriteLine($"?? Showing regular login form");
            ClearAllState();
            return Page();
        }

        private void ClearAllState()
        {
            Console.WriteLine($"?? Clearing all login state");

            // Clear model state
            ModelState.Clear();

            // Reset properties
            RequiresTwoFactor = false;
            TwoFactorEmail = null;
            TwoFactorUserId = null;
            TwoFactorCode = null;
            LoginData = new Login();

            // Clear Session
            HttpContext.Session.Remove("RequiresTwoFactor");
            HttpContext.Session.Remove("TwoFactorUserId");
            HttpContext.Session.Remove("TwoFactorEmail");
            HttpContext.Session.Remove("DeliveryMethod");

            // Clear TempData
            TempData.Remove("RequiresTwoFactor");
            TempData.Remove("TwoFactorUserId");
            TempData.Remove("TwoFactorEmail");
            TempData.Remove("DeliveryMethod");
            TempData.Remove("ResendMessage");
            TempData.Remove("ErrorMessage");
        }

        // ? REGULAR LOGIN HANDLER
        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine($"\n?? [LoginModel.OnPostAsync] Regular login attempt");
            Console.WriteLine($"?? Email submitted: {LoginData?.Email}");

            // Clear any 2FA data from this request
            TwoFactorCode = null;
            TwoFactorUserId = null;
            RequiresTwoFactor = false;

            if (!ModelState.IsValid)
            {
                Console.WriteLine("? Model validation failed for login form");
                return Page();
            }

            try
            {
                var user = await _userManager.FindByEmailAsync(LoginData.Email);
                if (user == null)
                {
                    Console.WriteLine($"? User not found: {LoginData.Email}");
                    ModelState.AddModelError(string.Empty, "Invalid email or password.");
                    return Page();
                }

                // Check if user is locked out (1 minute lockout)
                if (await _userManager.IsLockedOutAsync(user))
                {
                    var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                    var timeLeft = lockoutEnd.HasValue ? (lockoutEnd.Value - DateTimeOffset.UtcNow).TotalSeconds : 0;

                    Console.WriteLine($"? User locked out: {user.Email}, Time left: {timeLeft} seconds");
                    ModelState.AddModelError(string.Empty, $"Account is locked. Please try again in {Math.Ceiling(timeLeft / 60)} minute(s).");
                    return Page();
                }

                // Verify password
                var passwordValid = await _userManager.CheckPasswordAsync(user, LoginData.Password);
                if (!passwordValid)
                {
                    Console.WriteLine($"? Invalid password for: {user.Email}");

                    // Record failed attempt
                    await _userManager.AccessFailedAsync(user);

                    // Check failed attempts
                    var failedCount = await _userManager.GetAccessFailedCountAsync(user);
                    Console.WriteLine($"?? Failed attempts: {failedCount}");

                    // 1-minute lockout after 3 failed attempts
                    if (failedCount >= 3)
                    {
                        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(1));
                        ModelState.AddModelError(string.Empty, "Account locked due to 3 failed attempts. Please try again in 1 minute.");
                    }
                    else
                    {
                        var attemptsLeft = 3 - failedCount;
                        ModelState.AddModelError(string.Empty, $"Invalid email or password. {attemptsLeft} attempt(s) left before lockout.");
                    }

                    await _auditLogService.LogAsync(user.Id, user.Email, "LoginFailed", "Invalid password");
                    return Page();
                }

                // SUCCESSFUL PASSWORD - Check if 2FA is enabled
                Console.WriteLine($"? Password correct for: {user.Email}");

                // Reset failed count on successful password
                await _userManager.ResetAccessFailedCountAsync(user);

                var is2FAEnabled = await _twoFactorService.Is2FAEnabledAsync(user.Id);
                Console.WriteLine($"?? 2FA Status for {user.Email}: {is2FAEnabled}");

                if (is2FAEnabled)
                {
                    Console.WriteLine($"?? 2FA is enabled - generating and sending code...");

                    // Get 2FA settings to know delivery method
                    var settings = await _twoFactorService.Get2FASettingsAsync(user.Id);
                    Console.WriteLine($"?? Delivery method: {settings.PreferredMethod}");

                    // Generate and send 2FA code
                    var code = await _twoFactorService.GenerateAndSend2FACodeAsync(
                        user.Id,
                        "Login",
                        settings.PreferredMethod);

                    Console.WriteLine($"? 2FA code generated: {code}");

                    // Store in Session (more reliable than TempData for redirects)
                    HttpContext.Session.SetString("RequiresTwoFactor", "true");
                    HttpContext.Session.SetString("TwoFactorUserId", user.Id);
                    HttpContext.Session.SetString("TwoFactorEmail", user.Email);
                    HttpContext.Session.SetString("DeliveryMethod", settings.PreferredMethod);

                    Console.WriteLine($"?? Session set - UserId: {user.Id}, Email: {user.Email}");

                    // Redirect to GET to show 2FA form - pass userId as query parameter for reliability
                    return RedirectToPage(new { userId = user.Id });
                }
                else
                {
                    Console.WriteLine($"? 2FA not enabled - logging in directly...");

                    // Complete login without 2FA
                    await CompleteLoginAsync(user);
                    return RedirectToPage("/Index");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? ERROR in login: {ex.Message}");
                Console.WriteLine($"? Stack: {ex.StackTrace}");
                ModelState.AddModelError(string.Empty, "An error occurred during login.");
                return Page();
            }
        }

        // ? RESEND CODE HANDLER
        public async Task<IActionResult> OnPostResendCode()
        {
            Console.WriteLine($"\n?? [LoginModel.OnPostResendCode]");

            // Get user ID from Session
            var userId = HttpContext.Session.GetString("TwoFactorUserId");

            Console.WriteLine($"?? UserId from Session: {userId}");

            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine("? No user ID found for resend");
                TempData["ErrorMessage"] = "Cannot resend code. Please login again.";
                return RedirectToPage(new { reset = true });
            }

            try
            {
                // Get user info
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    Console.WriteLine($"? User not found: {userId}");
                    TempData["ErrorMessage"] = "User not found.";
                    return RedirectToPage(new { reset = true });
                }

                Console.WriteLine($"?? Resending code for user: {user.Email}");

                // Get current settings to know delivery method
                var settings = await _twoFactorService.Get2FASettingsAsync(userId);
                Console.WriteLine($"?? Delivery method: {settings.PreferredMethod}");

                // Generate and send new code
                var code = await _twoFactorService.GenerateAndSend2FACodeAsync(userId, "Login", settings.PreferredMethod);
                Console.WriteLine($"? New code sent: {code}");

                // Update Session
                HttpContext.Session.SetString("TwoFactorUserId", userId);
                HttpContext.Session.SetString("TwoFactorEmail", user.Email);
                HttpContext.Session.SetString("RequiresTwoFactor", "true");

                TempData["ResendMessage"] = "New verification code sent!";

                return RedirectToPage(new { userId = userId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Error resending code: {ex.Message}");
                Console.WriteLine($"? Stack trace: {ex.StackTrace}");
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
                return RedirectToPage(new { reset = true });
            }
        }

        // ? 2FA VERIFICATION (separate handler) - uses form parameter
        public async Task<IActionResult> OnPostVerifyTwoFactor([FromForm] string code)
        {
            Console.WriteLine($"\n? 2FA VERIFICATION:");
            Console.WriteLine($"Code entered: {code}");
            Console.WriteLine($"User ID from Session: {HttpContext.Session.GetString("TwoFactorUserId")}");

            // Get user ID from Session
            var userId = HttpContext.Session.GetString("TwoFactorUserId");

            if (string.IsNullOrEmpty(userId))
            {
                Console.WriteLine("? ERROR: No User ID found for 2FA verification!");
                TempData["ErrorMessage"] = "Session expired. Please login again.";
                return RedirectToPage(new { reset = true });
            }

            if (string.IsNullOrEmpty(code))
            {
                Console.WriteLine("? ERROR: No verification code entered!");

                // Set model state for error display
                RequiresTwoFactor = true;
                TwoFactorEmail = HttpContext.Session.GetString("TwoFactorEmail");
                TwoFactorUserId = userId;

                // Add error to ModelState
                ModelState.AddModelError("TwoFactorCode", "Verification code is required.");

                return Page();
            }

            var (isValid, message) = await _twoFactorService.Verify2FACodeAsync(
                userId, code, "Login");

            if (!isValid)
            {
                Console.WriteLine($"? 2FA verification failed: {message}");

                RequiresTwoFactor = true;
                TwoFactorUserId = userId;
                TwoFactorEmail = HttpContext.Session.GetString("TwoFactorEmail");

                ModelState.AddModelError("TwoFactorCode", message);

                return Page();
            }

            Console.WriteLine($"? 2FA verification successful!");

            var verifiedUser = await _userManager.FindByIdAsync(userId);
            if (verifiedUser != null)
            {
                await CompleteLoginAsync(verifiedUser);
                return RedirectToPage("/Index");
            }

            Console.WriteLine("? ERROR: User not found after 2FA verification");
            TempData["ErrorMessage"] = "User not found.";
            return RedirectToPage(new { reset = true });
        }

        private async Task CompleteLoginAsync(IdentityUser user)
        {
            Console.WriteLine($"? Login successful for: {user.Email}");

            // Reset failed attempts
            await _userManager.ResetAccessFailedCountAsync(user);

            // Log the successful login
            await _auditLogService.LogAsync(user.Id, user.Email, "Login", "User logged in successfully");

            // Update last login time for inactivity tracking
            var passwordService = HttpContext.RequestServices.GetService<IPasswordService>();
            if (passwordService != null)
            {
                await passwordService.UpdateLastLoginTimeAsync(user.Id);
            }

            // Create session record
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            var userAgent = HttpContext.Request.Headers["User-Agent"].ToString();

            var sessionToken = await _sessionManager.CreateSessionAsync(user.Id, ipAddress, userAgent);

            if (!string.IsNullOrEmpty(sessionToken))
            {
                Response.Cookies.Append("SessionToken", sessionToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTime.UtcNow.AddMinutes(30)
                });
            }

            // Sign in the user
            await _signInManager.SignInAsync(user, isPersistent: false);

            // Check for multiple sessions
            var activeSessions = await _sessionManager.GetActiveSessionCountAsync(user.Id);
            if (activeSessions > 1)
            {
                TempData["MultipleSessionsWarning"] = $"Warning: You're logged in from {activeSessions} devices/locations.";
            }

            // Check if password expired due to inactivity
            if (passwordService != null)
            {
                var isExpired = await passwordService.IsPasswordExpiredDueToInactivityAsync(user.Id);
                if (isExpired)
                {
                    TempData["PasswordExpiredWarning"] = "Your password has expired due to inactivity (5+ minutes since last login). Please change it.";
                }
            }

            Clear2FAStateOnly();
        }

        private void Clear2FAStateOnly()
        {
            Console.WriteLine($"?? Clearing only 2FA state");

            // Clear 2FA state
            HttpContext.Session.Remove("RequiresTwoFactor");
            HttpContext.Session.Remove("TwoFactorUserId");
            HttpContext.Session.Remove("TwoFactorEmail");
            HttpContext.Session.Remove("DeliveryMethod");

            // Clear TempData
            TempData.Remove("RequiresTwoFactor");
            TempData.Remove("TwoFactorUserId");
            TempData.Remove("TwoFactorEmail");
            TempData.Remove("DeliveryMethod");
            TempData.Remove("ResendMessage");
            TempData.Remove("ErrorMessage");

            // **DO NOT clear LoginData or ModelState here**
        }
    }
}