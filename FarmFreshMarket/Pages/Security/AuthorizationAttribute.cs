using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FarmFreshMarket.Pages.Security
{
    public class AuthorizeAttribute : Attribute, IPageFilter
    {
        public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
        {
            // Check if user is authenticated
            if (!context.HttpContext.User.Identity.IsAuthenticated)
            {
                // Redirect to login page
                context.Result = new RedirectToPageResult("/Login");
            }
        }

        public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
        {
            // Do nothing after page executes
        }

        public void OnPageHandlerSelected(PageHandlerSelectedContext context)
        {
            // Do nothing when handler is selected
        }
    }
}