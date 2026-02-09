using FarmFreshMarket.Models;
using FarmFreshMarket.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FarmFreshMarket.Pages
{
    [Authorize(Roles = "Admin")]
    public class AdminAuditModel : PageModel
    {
        private readonly IAuditLogService _auditLogService;

        public List<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

        public AdminAuditModel(IAuditLogService auditLogService)
        {
            _auditLogService = auditLogService;
        }

        public async Task OnGetAsync()
        {
            // Get recent 100 audit logs
            AuditLogs = await _auditLogService.GetRecentLogsAsync(100);
        }
    }
}