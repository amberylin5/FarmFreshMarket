using Microsoft.AspNetCore.Identity;
using System;
using System.ComponentModel.DataAnnotations;

namespace FarmFreshMarket.Models
{
    public class UserSession
    {
        [Key]
        public int SessionId { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; }

        [Required]
        [MaxLength(100)]
        public string SessionToken { get; set; }

        [Required]
        public DateTime LoginTime { get; set; }

        public DateTime? LastActivity { get; set; }

        public DateTime? LogoutTime { get; set; }

        [MaxLength(45)]
        public string IPAddress { get; set; }

        [MaxLength(500)]
        public string UserAgent { get; set; }

        [MaxLength(100)]
        public string DeviceInfo { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation property
        public virtual IdentityUser User { get; set; }
    }
}