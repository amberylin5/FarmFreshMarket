using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FarmFreshMarket.Services;

namespace FarmFreshMarket.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<LogoutModel> _logger;
        private readonly IAuditLogService _auditLogService;
        private readonly ISessionManagerService _sessionManager; // ADD THIS

        public LogoutModel(SignInManager<IdentityUser> signInManager,
                         ILogger<LogoutModel> logger,
                         IAuditLogService auditLogService,
                         ISessionManagerService sessionManager) // ADD THIS PARAMETER
        {
            _signInManager = signInManager;
            _logger = logger;
            _auditLogService = auditLogService;
            _sessionManager = sessionManager; // ADD THIS
        }

        public async Task<IActionResult> OnGet()
        {
            if (User.Identity.IsAuthenticated)
            {
                var userName = User.Identity.Name;
                var userId = _signInManager.UserManager.GetUserId(User);

                // Get current session token
                var sessionToken = Request.Cookies["SessionToken"];
                if (!string.IsNullOrEmpty(sessionToken))
                {
                    await _sessionManager.TerminateSessionAsync(sessionToken);
                }

                await _signInManager.SignOutAsync();

                // Clear session cookie
                Response.Cookies.Delete("SessionToken");

                await _auditLogService.LogAsync(userId, userName, "Logout", "User logged out successfully");
                _logger.LogInformation($"User {userName} logged out at {DateTime.Now}");
            }

            return RedirectToPage("Login");
        }
    }
}