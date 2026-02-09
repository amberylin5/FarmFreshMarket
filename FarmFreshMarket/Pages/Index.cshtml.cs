using FarmFreshMarket.Models;
using FarmFreshMarket.Security;
using FarmFreshMarket.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FarmFreshMarket.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AuthDbContext db;
        private readonly UserManager<IdentityUser> userManager;
        private readonly IPasswordService _passwordService;

        public Member LoggedInMember { get; set; }
        public bool IsPasswordExpired { get; set; }

        public IndexModel(AuthDbContext db,
                         UserManager<IdentityUser> userManager,
                         IPasswordService passwordService)
        {
            this.db = db;
            this.userManager = userManager;
            _passwordService = passwordService;
        }

        public async Task OnGetAsync()
        {
            if (User.Identity.IsAuthenticated)
            {
                var userId = userManager.GetUserId(User);
                LoggedInMember = db.Members.FirstOrDefault(m => m.UserId == userId);

                if (LoggedInMember != null)
                {
                    Console.WriteLine($"\n?? DEBUG - Retrieved from database (encoded):");
                    Console.WriteLine($"FullName: {LoggedInMember.FullName}");
                    Console.WriteLine($"AboutMe: {LoggedInMember.AboutMe}");
                    Console.WriteLine($"DeliveryAddress: {LoggedInMember.DeliveryAddress}");
                    Console.WriteLine($"LastLogin: {LoggedInMember.LastLoginTime}");

                    // Check if password expired due to inactivity
                    IsPasswordExpired = await _passwordService.IsPasswordExpiredDueToInactivityAsync(userId);

                    if (IsPasswordExpired)
                    {
                        Console.WriteLine($"?? Password expired due to inactivity!");
                        TempData["PasswordExpiredWarning"] = "Your password has expired due to inactivity (5+ minutes since last login). Please change it immediately.";
                    }

                    try
                    {
                        // Decrypt credit card
                        LoggedInMember.EncryptedCreditCardNo =
                            EncryptionHelper.Decrypt(LoggedInMember.EncryptedCreditCardNo);

                        // For security, only show last 4 digits
                        if (LoggedInMember.EncryptedCreditCardNo.Length > 4)
                        {
                            LoggedInMember.EncryptedCreditCardNo = "**** **** **** " +
                                LoggedInMember.EncryptedCreditCardNo.Substring(
                                    LoggedInMember.EncryptedCreditCardNo.Length - 4);
                        }
                        else
                        {
                            LoggedInMember.EncryptedCreditCardNo = "**** **** **** ****";
                        }
                    }
                    catch (Exception ex)
                    {
                        LoggedInMember.EncryptedCreditCardNo = "**** **** **** ****";
                        Console.WriteLine($"Decryption error: {ex.Message}");
                    }

                    Console.WriteLine($"Displaying encoded data for: {LoggedInMember.Email}");
                }
            }
        }
    }
}