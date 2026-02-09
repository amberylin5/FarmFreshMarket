using System.Text.RegularExpressions;

namespace FarmFreshMarket.Security
{
    public static class ValidationHelper
    {

        // 🔥 XSS PREVENTION: Remove dangerous HTML/script tags
        public static string SanitizeInput(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Remove script tags and event handlers
            string sanitized = Regex.Replace(input, @"<script.*?</script>", "",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            sanitized = Regex.Replace(sanitized, @"on\w+="".*?""", "",
                RegexOptions.IgnoreCase);

            sanitized = Regex.Replace(sanitized, @"javascript:", "",
                RegexOptions.IgnoreCase);

            sanitized = Regex.Replace(sanitized, @"<.*?>", ""); // Remove all HTML tags

            // Escape special characters
            sanitized = System.Net.WebUtility.HtmlEncode(sanitized);

            return sanitized.Trim();
        }

        // 🔥 SQL INJECTION PREVENTION: Check for dangerous SQL patterns
        public static bool ContainsSqlInjection(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            string[] sqlKeywords = {
                "--", ";", "'", "\"", "/*", "*/", "@@",
                "char", "nchar", "varchar", "nvarchar",
                "alter", "create", "delete", "drop", "exec", "execute",
                "insert", "select", "update", "union", "join"
            };

            string lowerInput = input.ToLower();

            foreach (var keyword in sqlKeywords)
            {
                if (lowerInput.Contains(keyword))
                    return true;
            }

            return false;
        }

        // 🔥 VALIDATE EMAIL
        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrEmpty(email))
                return false;

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

        // 🔥 VALIDATE PHONE NUMBER (Singapore format)
        public static bool IsValidPhone(string phone)
        {
            if (string.IsNullOrEmpty(phone))
                return false;

            // Singapore phone: 8 digits starting with 8 or 9
            return Regex.IsMatch(phone, @"^[89]\d{7}$");
        }

        // 🔥 VALIDATE CREDIT CARD (Luhn algorithm)
        public static bool IsValidCreditCard(string cardNumber)
        {
            if (string.IsNullOrEmpty(cardNumber))
                return false;

            cardNumber = cardNumber.Replace(" ", "").Replace("-", "");

            if (!Regex.IsMatch(cardNumber, @"^\d{13,16}$"))
                return false;

            // Luhn algorithm check
            int sum = 0;
            bool alternate = false;

            for (int i = cardNumber.Length - 1; i >= 0; i--)
            {
                int n = int.Parse(cardNumber[i].ToString());

                if (alternate)
                {
                    n *= 2;
                    if (n > 9) n = (n % 10) + 1;
                }

                sum += n;
                alternate = !alternate;
            }

            return (sum % 10 == 0);
        }

        // 🔥 VALIDATE FILE EXTENSION
        public static bool IsValidImageFile(string fileName, string[] allowedExtensions)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLower();
            return allowedExtensions.Contains(extension);
        }

        // 🔥 PREVENT CROSS-SITE REQUEST FORGERY (CSRF) - Already handled by ASP.NET
        // Just ensure [ValidateAntiForgeryToken] is used
    }
}