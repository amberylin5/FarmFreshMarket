using System.ComponentModel.DataAnnotations;

namespace FarmFreshMarket.ViewModels
{
    public class Register
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be 2-100 characters")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Credit card number is required")]
        [RegularExpression(@"^\d{13,16}$", ErrorMessage = "Enter a valid 13-16 digit credit card number")]
        public string CreditCardNo { get; set; }

        [Required(ErrorMessage = "Please select gender")]
        public string Gender { get; set; }

        [Required(ErrorMessage = "Mobile number is required")]
        [RegularExpression(@"^\d{8,}$", ErrorMessage = "Enter a valid mobile number (8+ digits)")]
        public string MobileNo { get; set; }

        [Required(ErrorMessage = "Delivery address is required")]
        [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters")]
        public string DeliveryAddress { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [MinLength(12, ErrorMessage = "Password must be at least 12 characters")]
        public string Password { get; set; }

        [Required(ErrorMessage = "Please confirm your password")]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Profile photo is required")]
        public IFormFile Photo { get; set; }

        [Required(ErrorMessage = "Please tell us about yourself")]
        [StringLength(500, ErrorMessage = "About me cannot exceed 500 characters")]
        public string AboutMe { get; set; }
    }
}