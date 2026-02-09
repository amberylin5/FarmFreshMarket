using FarmFreshMarket.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace FarmFreshMarket.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IAuditLogService _auditLogService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        [BindProperty]
        public InputModel Input { get; set; }

        public ForgotPasswordModel(
            UserManager<IdentityUser> userManager,
            IEmailService emailService,
            IAuditLogService auditLogService,
            IHttpContextAccessor httpContextAccessor)
        {
            _userManager = userManager;
            _emailService = emailService;
            _auditLogService = auditLogService;
            _httpContextAccessor = httpContextAccessor;
        }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required")]
            [EmailAddress(ErrorMessage = "Invalid email address")]
            [Display(Name = "Email")]
            public string Email { get; set; }
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user == null)
            {
                TempData["SuccessMessage"] = "If your email is registered, you will receive a password reset link.";
                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            // Generate password reset token
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            // ? FIX: Encode properly for URL
            var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            var encodedEmail = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(Input.Email));

            // ? FIX: Build the URL correctly
            var resetUrl = $"/ResetPassword?email={encodedEmail}&token={encodedToken}";

            // For display purposes (in email or console)
            var fullResetUrl = $"{Request.Scheme}://{Request.Host}{resetUrl}";

            Console.WriteLine($"\n?? RESET LINK GENERATED:");
            Console.WriteLine($"Encoded Email: {encodedEmail}");
            Console.WriteLine($"Encoded Token: {encodedToken}");
            Console.WriteLine($"Reset URL: {resetUrl}");
            Console.WriteLine($"Full URL: {fullResetUrl}\n");

            // Send email
            await _emailService.SendPasswordResetEmailAsync(Input.Email, fullResetUrl);

            // Store for testing
            TempData["TestResetLink"] = fullResetUrl;
            TempData["EncodedEmail"] = encodedEmail;
            TempData["EncodedToken"] = encodedToken;
            TempData["SuccessMessage"] = "If your email is registered, you will receive a password reset link.";

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}