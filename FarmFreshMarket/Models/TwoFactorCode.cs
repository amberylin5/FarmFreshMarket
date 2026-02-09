using System.ComponentModel.DataAnnotations;

public class TwoFactorCode
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; }

    [Required]
    [MaxLength(10)]
    public string Code { get; set; }

    [Required]
    public string Purpose { get; set; } // "Login", "ResetPassword", "Enable2FA"

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ExpiresAt { get; set; }

    public bool IsUsed { get; set; } = false;

    public DateTime? UsedAt { get; set; }

    [MaxLength(45)]
    public string IPAddress { get; set; }

    [MaxLength(500)]
    public string UserAgent { get; set; }

    [MaxLength(20)]
    public string DeliveryMethod { get; set; } = "Email"; // ADD DEFAULT VALUE HERE
}