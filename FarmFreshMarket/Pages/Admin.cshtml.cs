using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FarmFreshMarket.Pages
{
    [Authorize(Roles = "Admin")] // Only Admins can access
    public class AdminModel : PageModel
    {
        public IActionResult OnGet()
        {
            // Optional: If you want to explicitly handle non-admins
            if (!User.IsInRole("Admin"))
            {
                // This will trigger the AccessDeniedPath
                return Forbid();
            }

            return Page();
        }
    }
}