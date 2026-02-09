using FarmFreshMarket.Models;
using FarmFreshMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FarmFreshMarket.Pages
{
    [Authorize]
    public class ManageSessionsModel : PageModel
    {
        private readonly ISessionManagerService _sessionManager;
        private readonly IAuditLogService _auditLogService;
        private readonly Microsoft.AspNetCore.Identity.UserManager<IdentityUser> _userManager;

        public List<UserSession> ActiveSessions { get; set; } = new List<UserSession>();
        public UserSession CurrentSession { get; set; }
        public List<UserSession> OtherSessions { get; set; } = new List<UserSession>();
        public bool HasMultipleSessions => ActiveSessions.Count > 1;

        public ManageSessionsModel(ISessionManagerService sessionManager,
                                 IAuditLogService auditLogService,
                                 Microsoft.AspNetCore.Identity.UserManager<IdentityUser> userManager)
        {
            _sessionManager = sessionManager;
            _auditLogService = auditLogService;
            _userManager = userManager;
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                ActiveSessions = await _sessionManager.GetActiveSessionsAsync(user.Id);

                // Get current session token from cookie
                var currentSessionToken = Request.Cookies["SessionToken"];

                if (!string.IsNullOrEmpty(currentSessionToken))
                {
                    CurrentSession = ActiveSessions.FirstOrDefault(s => s.SessionToken == currentSessionToken);
                    OtherSessions = ActiveSessions.Where(s => s.SessionToken != currentSessionToken).ToList();
                }
                else
                {
                    CurrentSession = ActiveSessions.FirstOrDefault();
                    OtherSessions = ActiveSessions.Skip(1).ToList();
                }
            }
        }

        public async Task<IActionResult> OnPostTerminateSessionAsync(string sessionToken)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var success = await _sessionManager.TerminateSessionAsync(sessionToken);
                if (success)
                {
                    await _auditLogService.LogAsync(user.Id, user.Email, "SessionTerminated",
                        "User terminated a session from another device", true);

                    TempData["SuccessMessage"] = "Session terminated successfully.";
                }
            }

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostTerminateAllOtherAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var currentSessionToken = Request.Cookies["SessionToken"];
                var success = await _sessionManager.TerminateAllOtherSessionsAsync(user.Id, currentSessionToken);

                if (success)
                {
                    await _auditLogService.LogAsync(user.Id, user.Email, "AllSessionsTerminated",
                        "User logged out from all other devices", true);

                    TempData["SuccessMessage"] = "Logged out from all other devices.";
                }
            }

            return RedirectToPage();
        }
    }
}