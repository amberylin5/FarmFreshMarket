using FarmFreshMarket.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FarmFreshMarket.Pages
{
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly IPasswordService _passwordService;
        private readonly IAuditLogService _auditLogService;

        [BindProperty]
        public InputModel Input { get; set; }

        public string PasswordAgeMessage { get; set; }
        public int CurrentPasswordAgeMinutes { get; set; }

        public ChangePasswordModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            IPasswordService passwordService,
            IAuditLogService auditLogService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _passwordService = passwordService;
            _auditLogService = auditLogService;
        }

        public class InputModel
        {
            [Required(ErrorMessage = "Current password is required")]
            [DataType(DataType.Password)]
            [Display(Name = "Current Password")]
            public string CurrentPassword { get; set; }

            [Required(ErrorMessage = "New password is required")]
            [DataType(DataType.Password)]
            [MinLength(12, ErrorMessage = "Password must be at least 12 characters")]
            [Display(Name = "New Password")]
            public string NewPassword { get; set; }

            [Required(ErrorMessage = "Please confirm your new password")]
            [DataType(DataType.Password)]
            [Compare("NewPassword", ErrorMessage = "Passwords do not match")]
            [Display(Name = "Confirm New Password")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                // Check password change eligibility
                var eligibility = await _passwordService.CheckPasswordChangeEligibilityAsync(user.Id);
                PasswordAgeMessage = eligibility.errorMessage;

                // Get current password age in minutes
                CurrentPasswordAgeMinutes = await _passwordService.GetPasswordAgeInMinutesAsync(user.Id);
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "User not found.");
                return Page();
            }

            // 1. Verify current password
            var verifyResult = await _userManager.CheckPasswordAsync(user, Input.CurrentPassword);
            if (!verifyResult)
            {
                ModelState.AddModelError(string.Empty, "Current password is incorrect.");
                return Page();
            }

            // 2. Check password change eligibility (minimum 2 minutes)
            var canChange = await _passwordService.CanChangePasswordAsync(user.Id);
            if (!canChange)
            {
                var lastChange = await _passwordService.GetLastPasswordChangeDateAsync(user.Id);
                if (lastChange.HasValue)
                {
                    var minutesLeft = (TimeSpan.FromMinutes(2) - (DateTime.UtcNow - lastChange.Value)).TotalMinutes;
                    ModelState.AddModelError(string.Empty,
                        $"You cannot change your password yet. Please wait {Math.Ceiling(minutesLeft)} more minutes.");
                }
                return Page();
            }

            // 3. Check if new password is in history (last 2 passwords)
            var isInHistory = await _passwordService.IsPasswordInHistoryAsync(user.Id, Input.NewPassword);
            if (isInHistory)
            {
                ModelState.AddModelError(string.Empty,
                    "You cannot reuse your last 2 passwords. Please choose a different password.");
                return Page();
            }

            // 4. Validate password complexity
            var isValidComplexity = await _passwordService.ValidatePasswordComplexityAsync(Input.NewPassword);
            if (!isValidComplexity)
            {
                ModelState.AddModelError(string.Empty,
                    "Password does not meet complexity requirements. See requirements above.");
                return Page();
            }

            // 5. Change password
            var changeResult = await _userManager.ChangePasswordAsync(user, Input.CurrentPassword, Input.NewPassword);

            if (!changeResult.Succeeded)
            {
                foreach (var error in changeResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return Page();
            }

            // 6. Add to password history
            var newPasswordHash = _userManager.PasswordHasher.HashPassword(user, Input.NewPassword);
            await _passwordService.AddToPasswordHistoryAsync(user.Id, newPasswordHash, "User");

            // 7. Check if password was expired
            var passwordAge = await _passwordService.GetPasswordAgeInMinutesAsync(user.Id);
            if (passwordAge > 5)
            {
                TempData["PasswordExpiredMessage"] = "Your password was expired (over 5 minutes old). It has been renewed.";
            }

            // 8. Log the action
            await _auditLogService.LogAsync(user.Id, user.Email, "PasswordChanged",
                "User changed password successfully", true);

            // 9. Sign out and redirect to login
            await _signInManager.SignOutAsync();

            TempData["SuccessMessage"] = "Password changed successfully! Please login with your new password.";
            return RedirectToPage("/Login");
        }
    }
}