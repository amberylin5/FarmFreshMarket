using FarmFreshMarket.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System.Threading.Tasks;

namespace FarmFreshMarket.Middleware
{
    public class PasswordExpiryMiddleware
    {
        private readonly RequestDelegate _next;

        public PasswordExpiryMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IPasswordService passwordService)
        {
            // Check if user is authenticated
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userManager = context.RequestServices.GetService<UserManager<IdentityUser>>();
                if (userManager != null)
                {
                    var user = await userManager.GetUserAsync(context.User);
                    if (user != null)
                    {
                        // Check if password expired due to inactivity (5 minutes)
                        var isExpired = await passwordService.IsPasswordExpiredDueToInactivityAsync(user.Id);

                        if (isExpired)
                        {
                            // Only set warning if not already on ChangePassword page
                            if (!context.Request.Path.Value.Contains("/ChangePassword"))
                            {
                                context.Items["PasswordExpired"] = true;
                            }
                        }
                    }
                }
            }

            await _next(context);
        }
    }
}