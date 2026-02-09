using System.ComponentModel.DataAnnotations;

public class User2FASettings
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; }

    public bool IsEnabled { get; set; } = false;

    public DateTime? EnabledAt { get; set; }

    [MaxLength(20)]
    public string PreferredMethod { get; set; } = "Email"; // Default value

    [EmailAddress]
    public string Email { get; set; } = ""; // Default empty string

    [Phone]
    public string PhoneNumber { get; set; } = ""; // Default empty string

    public bool IsPhoneVerified { get; set; } = false;

    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    public int FailedAttempts { get; set; } = 0;

    public DateTime? LockoutUntil { get; set; }
}