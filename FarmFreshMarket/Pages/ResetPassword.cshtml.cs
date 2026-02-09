using FarmFreshMarket.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FarmFreshMarket.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IPasswordService _passwordService;
        private readonly IAuditLogService _auditLogService;
        private readonly ILogger<ResetPasswordModel> _logger;

        public ResetPasswordModel(
            UserManager<IdentityUser> userManager,
            IPasswordService passwordService,
            IAuditLogService auditLogService,
            ILogger<ResetPasswordModel> logger)
        {
            _userManager = userManager;
            _passwordService = passwordService;
            _auditLogService = auditLogService;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public bool InvalidToken { get; set; }

        public class InputModel
        {
            [Required]
            public string Email { get; set; }

            [Required]
            public string Token { get; set; }

            [Required(ErrorMessage = "New password is required")]
            [DataType(DataType.Password)]
            [Display(Name = "New Password")]
            public string NewPassword { get; set; }

            [Required(ErrorMessage = "Please confirm your new password")]
            [DataType(DataType.Password)]
            [Display(Name = "Confirm New Password")]
            [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
            public string ConfirmPassword { get; set; }
        }

        public IActionResult OnGet(string email, string token)
        {
            Console.WriteLine($"\n?? RESET PASSWORD ACCESSED:");
            Console.WriteLine($"Raw Email: {email}");
            Console.WriteLine($"Raw Token: {token}");

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            {
                InvalidToken = true;
                return Page();
            }

            try
            {
                // ? FIX: Add back padding if needed and replace URL-safe characters
                email = email.Replace("-", "+").Replace("_", "/");
                token = token.Replace("-", "+").Replace("_", "/");

                // Add padding if needed
                var padding = email.Length % 4;
                if (padding != 0) email += new string('=', 4 - padding);

                padding = token.Length % 4;
                if (padding != 0) token += new string('=', 4 - padding);

                Console.WriteLine($"After cleaning - Email: {email}");
                Console.WriteLine($"After cleaning - Token: {token}");

                var decodedEmail = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(email));
                var decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));

                Console.WriteLine($"? Successfully decoded:");
                Console.WriteLine($"User Email: {decodedEmail}");
                Console.WriteLine($"Token Length: {decodedToken.Length}");

                Input = new InputModel
                {
                    Email = decodedEmail,
                    Token = decodedToken
                };

                return Page();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"? Decoding failed: {ex.Message}");
                InvalidToken = true;
                ModelState.AddModelError(string.Empty, "Invalid or expired reset link.");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            Console.WriteLine($"\n?? RESET PASSWORD SUBMITTED:");
            Console.WriteLine($"Email: {Input.Email}");
            Console.WriteLine($"Token Length: {Input.Token.Length}");

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                // Don't reveal that the user does not exist
                TempData["SuccessMessage"] = "Your password has been reset successfully.";
                return RedirectToPage("/ResetPasswordConfirmation");
            }

            // Check if new password is in history
            var isInHistory = await _passwordService.IsPasswordInHistoryAsync(user.Id, Input.NewPassword);
            if (isInHistory)
            {
                ModelState.AddModelError(string.Empty, "You cannot reuse your last 2 passwords. Please choose a different password.");
                return Page();
            }

            // Validate password complexity
            var isValidComplexity = await _passwordService.ValidatePasswordComplexityAsync(Input.NewPassword);
            if (!isValidComplexity)
            {
                ModelState.AddModelError(string.Empty, "Password does not meet complexity requirements.");
                return Page();
            }

            // Reset password
            var result = await _userManager.ResetPasswordAsync(user, Input.Token, Input.NewPassword);
            if (!result.Succeeded)
            {
                Console.WriteLine($"? Password reset failed:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"  - {error.Description}");
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // Add to password history
            var passwordHash = _userManager.PasswordHasher.HashPassword(user, Input.NewPassword);
            await _passwordService.AddToPasswordHistoryAsync(user.Id, passwordHash, "Reset");

            // Log the password reset
            await _auditLogService.LogAsync(
                userId: user.Id,
                userEmail: user.Email,
                action: "PasswordReset",
                description: "User reset password via reset link",
                success: true
            );

            _logger.LogInformation($"User {user.Email} reset their password via reset link.");

            Console.WriteLine($"? Password reset successful for: {user.Email}");

            TempData["SuccessMessage"] = "Your password has been reset successfully! Please login with your new password.";
            return RedirectToPage("/ResetPasswordConfirmation");
        }
    }
}