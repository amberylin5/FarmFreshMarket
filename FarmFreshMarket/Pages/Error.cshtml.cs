using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FarmFreshMarket.Pages
{
    public class ErrorModel : PageModel
    {
        public int StatusCode { get; set; }
        public string ErrorMessage { get; set; }

        public void OnGet(int? statusCode = null)
        {
            // Try to get status code from multiple sources
            if (statusCode.HasValue)
            {
                StatusCode = statusCode.Value;
            }
            else if (HttpContext.Request.Query.ContainsKey("statusCode"))
            {
                if (int.TryParse(HttpContext.Request.Query["statusCode"], out int code))
                {
                    StatusCode = code;
                }
                else
                {
                    StatusCode = 500;
                }
            }
            else if (HttpContext.Response.StatusCode != 200)
            {
                // Get from Response
                StatusCode = HttpContext.Response.StatusCode;
            }
            else
            {
                // Default to 404 for fallback routes (like MapFallbackToPage)
                StatusCode = 404;
            }

            // Set appropriate error message
            ErrorMessage = StatusCode switch
            {
                404 => "The page you're looking for doesn't exist.",
                403 => "You don't have permission to access this page.",
                500 => "An internal server error occurred.",
                _ => $"An error occurred (Status Code: {StatusCode})."
            };

            // Ensure response has the correct status code
            Response.StatusCode = StatusCode;
        }
    }
}