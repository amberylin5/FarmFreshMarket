using System.ComponentModel.DataAnnotations;

namespace FarmFreshMarket.Models
{
    public class PasswordPolicy
    {
        [Key]
        public int PolicyId { get; set; }

        [Required]
        [MaxLength(100)]
        public string PolicyName { get; set; } = "Default";

        // Changed to minutes for testing/demo
        [Range(1, 1440)] // 1 minute to 24 hours (1440 minutes)
        public int MinimumPasswordAgeMinutes { get; set; } = 2; // Can't change password within 2 minutes

        [Range(5, 43200)] // 5 minutes to 30 days (43200 minutes)
        public int MaximumPasswordAgeMinutes { get; set; } = 5; // Must change after 5 minutes

        [Range(1, 10)]
        public int PasswordHistorySize { get; set; } = 2; // Remember last 2 passwords

        [Range(12, 50)]
        public int MinimumPasswordLength { get; set; } = 12;

        public bool RequireUppercase { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireDigit { get; set; } = true;
        public bool RequireSpecialChar { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public DateTime? LastUpdated { get; set; }
    }
}