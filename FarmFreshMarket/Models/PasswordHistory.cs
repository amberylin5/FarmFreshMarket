using System;
using System.ComponentModel.DataAnnotations;

namespace FarmFreshMarket.Models
{
    public class PasswordHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(450)]
        public string UserId { get; set; }

        [Required]
        [MaxLength(256)] // For hashed password
        public string PasswordHash { get; set; }

        [Required]
        public DateTime ChangedDate { get; set; }

        [MaxLength(100)]
        public string ChangedBy { get; set; } // "User", "Admin", "System"

        public bool IsCurrent { get; set; } = false;
    }
}