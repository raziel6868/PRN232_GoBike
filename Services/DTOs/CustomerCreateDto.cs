using System.ComponentModel.DataAnnotations;

namespace Services.DTOs;

public class CustomerCreateDto
{
    public const string DefaultPassword = "12345678";

    [Required]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Username may only contain letters, numbers, dots, underscores, and hyphens")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[0-9]{12}$", ErrorMessage = "CCCD must be exactly 12 digits")]
    [StringLength(12, MinimumLength = 12)]
    public string CCCD { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^0[0-9]{9,10}$", ErrorMessage = "Invalid Vietnamese phone format")]
    [MaxLength(15)]
    public string PhoneNumber { get; set; } = string.Empty;

    [EmailAddress]
    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(255)]
    public string? Address { get; set; }

    [Required]
    public DateTime DateOfBirth { get; set; }

    [Required(ErrorMessage = "Driver license is required")]
    [MaxLength(20)]
    public string DriverLicenseNo { get; set; } = string.Empty;
}
