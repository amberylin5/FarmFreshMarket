using System;
using System.ComponentModel.DataAnnotations;

namespace FarmFreshMarket.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)] // Increased for UserId (can be GUID)
        public string UserId { get; set; }

        [Required]
        [MaxLength(256)] // Email can be up to 254 chars, 256 is safe
        public string UserEmail { get; set; }

        [Required]
        [MaxLength(50)]
        public string Action { get; set; } // "Login", "Logout", "Register", "FailedLogin", "PasswordChange", "AccessDenied"

        [MaxLength(500)]
        public string Description { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }

        [MaxLength(45)]
        public string IPAddress { get; set; }

        [MaxLength(500)] // CHANGED: Increased from 100 to 500 for long UserAgent strings
        public string UserAgent { get; set; }

        public bool Success { get; set; } = true;

        [MaxLength(1000)] // CHANGED: Increased from 500 to 1000
        public string AdditionalInfo { get; set; } = string.Empty;
    }
}