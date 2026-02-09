using FarmFreshMarket.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FarmFreshMarket.Pages
{
    public class AccessDeniedModel : PageModel
    {
        private readonly IAuditLogService _auditLogService;
        private readonly UserManager<IdentityUser> _userManager;

        public AccessDeniedModel(IAuditLogService auditLogService, UserManager<IdentityUser> userManager)
        {
            _auditLogService = auditLogService;
            _userManager = userManager;
        }

        public async Task OnGet()
        {
            // Set status code to 403
            Response.StatusCode = 403;

            // Log the access denied attempt
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    await _auditLogService.LogAsync(
                        userId: user.Id,
                        userEmail: user.Email,
                        action: "AccessDenied",
                        description: $"Access denied to {Request.Path}",
                        success: false,
                        additionalInfo: $"403 Forbidden - User tried to access {Request.Path}"
                    );
                }
            }
        }
    }
}