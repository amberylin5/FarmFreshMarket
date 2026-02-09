using System.ComponentModel.DataAnnotations;

namespace FarmFreshMarket.Models
{
    public class Member
    {
        [Key]
        public int MemberId { get; set; }

        [Required]
        public string FullName { get; set; }

        [Required]
        public string EncryptedCreditCardNo { get; set; }

        public string Gender { get; set; }

        public string MobileNo { get; set; }

        public string DeliveryAddress { get; set; }

        [Required]
        public string Email { get; set; }

        public string PhotoPath { get; set; }

        public string AboutMe { get; set; }

        public string UserId { get; set; }

        // ADD THIS: Track last login time for password expiry based on inactivity
        public DateTime LastLoginTime { get; set; } = DateTime.UtcNow;
    }
}