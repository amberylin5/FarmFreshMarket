using FarmFreshMarket.Models;
using FarmFreshMarket.Security;
using FarmFreshMarket.Services;
using FarmFreshMarket.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.RegularExpressions;

namespace FarmFreshMarket.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly UserManager<IdentityUser> userManager;
        private readonly SignInManager<IdentityUser> signInManager;
        private readonly AuthDbContext db;
        private readonly IAuditLogService _auditLogService;

        [BindProperty]
        public Register RModel { get; set; }

        public RegisterModel(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            AuthDbContext db,
            IAuditLogService auditLogService)
        {
            this.userManager = userManager;
            this.signInManager = signInManager;
            this.db = db;
            _auditLogService = auditLogService;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            Console.WriteLine(">>> REGISTER POST START <<<");

            if (!ModelState.IsValid)
            {
                Console.WriteLine(">>> CLIENT VALIDATION FAILED <<<");
                return Page();
            }

            // DEBUG: Log what user entered for XSS testing
            Console.WriteLine($"\n>>> DEBUG - User Input for XSS Testing:");
            Console.WriteLine($"FullName: {RModel.FullName}");
            Console.WriteLine($"AboutMe: {RModel.AboutMe}");
            Console.WriteLine($"DeliveryAddress: {RModel.DeliveryAddress}\n");

            // SERVER-SIDE VALIDATION
            var validationErrors = ValidateAllInputs();
            if (validationErrors.Any())
            {
                foreach (var error in validationErrors)
                    ModelState.AddModelError(error.Key, error.Value);

                Console.WriteLine(">>> SERVER VALIDATION FAILED <<<");
                return Page();
            }

            // reCAPTCHA check
            var recaptchaToken = Request.Form["g-recaptcha-response"].ToString();
            if (string.IsNullOrEmpty(recaptchaToken))
            {
                ModelState.AddModelError("", "Security check failed.");
                return Page();
            }

            // Check duplicate email
            var existingUser = await userManager.FindByEmailAsync(RModel.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError("RModel.Email", "Email already registered.");
                return Page();
            }

            // Create user
            var user = new IdentityUser
            {
                UserName = RModel.Email,
                Email = RModel.Email
            };

            var result = await userManager.CreateAsync(user, RModel.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return Page();
            }

            Console.WriteLine(">>> USER CREATED <<<");

            // Photo upload
            string fileName = "";
            if (RModel.Photo != null)
            {
                string uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                fileName = Guid.NewGuid().ToString() + Path.GetExtension(RModel.Photo.FileName);
                string filePath = Path.Combine(uploadsFolder, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await RModel.Photo.CopyToAsync(fileStream);
                }
            }
            else
            {
                ModelState.AddModelError("RModel.Photo", "Photo required");
                return Page();
            }

            // ? ENCODE ALL TEXT FIELDS BEFORE SAVING TO DATABASE
            // This allows XSS scripts to be saved but in encoded form
            // They will display as text, not execute as code
            Member member = new Member
            {
                // Encode text fields to prevent XSS when displaying
                FullName = System.Net.WebUtility.HtmlEncode(RModel.FullName?.Trim() ?? ""),
                Email = RModel.Email, // Email doesn't need encoding
                Gender = System.Net.WebUtility.HtmlEncode(RModel.Gender?.Trim() ?? ""),
                MobileNo = RModel.MobileNo, // Phone number doesn't need encoding
                DeliveryAddress = System.Net.WebUtility.HtmlEncode(RModel.DeliveryAddress?.Trim() ?? ""),
                EncryptedCreditCardNo = EncryptionHelper.Encrypt(RModel.CreditCardNo?.Replace(" ", "").Replace("-", "") ?? ""),
                AboutMe = System.Net.WebUtility.HtmlEncode(RModel.AboutMe?.Trim() ?? ""),
                PhotoPath = "/uploads/" + fileName,
                UserId = user.Id,
                LastLoginTime = DateTime.UtcNow
            };

            db.Members.Add(member);
            await db.SaveChangesAsync();

            await _auditLogService.LogAsync(
                userId: user.Id,
                userEmail: user.Email,
                action: "Registration",
                description: "New user registered successfully",
                additionalInfo: $"Name: {RModel.FullName}"
            );

            Console.WriteLine(">>> MEMBER SAVED (ENCODED FOR XSS PROTECTION) <<<");
            Console.WriteLine($"FullName stored as: {member.FullName}");
            Console.WriteLine($"AboutMe stored as: {member.AboutMe}");

            // Sign in
            await signInManager.SignInAsync(user, false);
            return RedirectToPage("Index");
        }

        // WORKING VALIDATION METHOD
        private Dictionary<string, string> ValidateAllInputs()
        {
            var errors = new Dictionary<string, string>();

            // 1. EMAIL VALIDATION
            if (!IsValidEmail(RModel.Email))
                errors.Add("RModel.Email", "Invalid email format.");

            // 2. PHONE VALIDATION (Singapore: 8 digits, starts with 8 or 9)
            if (!IsValidSingaporePhone(RModel.MobileNo))
                errors.Add("RModel.MobileNo", "Invalid Singapore mobile number. Must be 8 digits starting with 8 or 9.");

            // 3. CREDIT CARD VALIDATION (simple length check)
            if (!IsValidCreditCardSimple(RModel.CreditCardNo))
                errors.Add("RModel.CreditCardNo", "Invalid credit card. Must be 13-16 digits.");

            // 4. PASSWORD VALIDATION
            var passwordCheck = ValidatePassword(RModel.Password);
            if (!passwordCheck.isValid)
                errors.Add("RModel.Password", passwordCheck.errorMessage);

            // 5. SQL INJECTION DETECTION (LENIENT - ALLOWS QUOTES FOR XSS)
            // We allow quotes and script tags for XSS demonstration
            if (ContainsSqlInjection(RModel.FullName) ||
                ContainsSqlInjection(RModel.DeliveryAddress) ||
                ContainsSqlInjection(RModel.AboutMe))
            {
                // More specific error message
                errors.Add("", "Input contains dangerous SQL patterns. Please avoid SQL keywords like SELECT, INSERT, DROP, etc.");
            }

            return errors;
        }

        // Helper validation methods
        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private bool IsValidSingaporePhone(string phone)
        {
            // Remove spaces, dashes
            phone = phone.Replace(" ", "").Replace("-", "");

            // Must be 8 digits, starting with 8 or 9
            return Regex.IsMatch(phone, @"^[89]\d{7}$");
        }

        private bool IsValidCreditCardSimple(string card)
        {
            // Remove spaces and dashes
            card = card.Replace(" ", "").Replace("-", "");

            // Check length and all digits
            return card.Length >= 13 && card.Length <= 16 && card.All(char.IsDigit);
        }

        private bool ContainsSqlInjection(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;

            // LENIENT CHECK - ALLOWS QUOTES (' and ") FOR XSS DEMONSTRATION
            // ONLY block clear SQL injection patterns without quotes
            string[] dangerousPatterns = {
                ";--",          // SQL comment injection
                ";/*",          // Block comment injection
                "@@",           // SQL Server global variable
                "exec(", "execute(",
                "select(", "insert(", "update(", "delete(", "drop(", "alter(", "create(",
                "union(",
                " or(", " and("
            };

            string lowerInput = input.ToLower();

            foreach (var pattern in dangerousPatterns)
            {
                if (lowerInput.Contains(pattern))
                {
                    Console.WriteLine($"? Blocked SQL pattern: '{pattern}' in input: {input}");
                    return true;
                }
            }

            // Also check for SQL keywords followed by spaces (more lenient)
            string[] sqlKeywordsWithSpace = {
                "exec ", "execute ", "select ", "insert ", "update ", "delete ",
                "drop ", "alter ", "create ", "union ", " or ", " and "
            };

            foreach (var keyword in sqlKeywordsWithSpace)
            {
                if (lowerInput.Contains(keyword))
                {
                    // Check if it's likely SQL (not part of normal text)
                    int index = lowerInput.IndexOf(keyword);
                    if (index > 0)
                    {
                        char before = lowerInput[index - 1];
                        if (char.IsWhiteSpace(before) || before == '(' || before == ';')
                        {
                            Console.WriteLine($"? Blocked SQL keyword: '{keyword}' in input: {input}");
                            return true;
                        }
                    }
                }
            }

            // Block dangerous SQL injection attempts that combine ; with commands
            if (lowerInput.Contains(";") &&
                (lowerInput.Contains("drop ") ||
                 lowerInput.Contains("delete ") ||
                 lowerInput.Contains("update ") ||
                 lowerInput.Contains("alter ") ||
                 lowerInput.Contains("create ")))
            {
                Console.WriteLine($"? Blocked dangerous SQL combination in input: {input}");
                return true;
            }

            // ? ALLOW quotes and script tags for XSS demonstration
            Console.WriteLine($"? Input passed SQL injection check: {input}");
            return false;
        }

        private (bool isValid, string errorMessage) ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return (false, "Password is required");

            var errors = new List<string>();

            if (password.Length < 12)
                errors.Add("must be at least 12 characters");

            if (!password.Any(char.IsUpper))
                errors.Add("must contain uppercase letter");

            if (!password.Any(char.IsLower))
                errors.Add("must contain lowercase letter");

            if (!password.Any(char.IsDigit))
                errors.Add("must contain number");

            if (!password.Any(ch => !char.IsLetterOrDigit(ch)))
                errors.Add("must contain special character");

            if (errors.Any())
                return (false, "Password " + string.Join(", ", errors) + ".");

            return (true, "Password is strong");
        }

        // Optional: Add a test method to demonstrate XSS encoding
        public string TestXSSEncoding(string input)
        {
            // This method shows how XSS scripts are encoded
            if (string.IsNullOrEmpty(input))
                return "No input";

            var encoded = System.Net.WebUtility.HtmlEncode(input);
            return $"Original: {input}\nEncoded: {encoded}\nSafe to display: {encoded}";
        }
    }
}