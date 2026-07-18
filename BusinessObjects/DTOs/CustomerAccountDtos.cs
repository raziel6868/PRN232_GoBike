using System.ComponentModel.DataAnnotations;

namespace BusinessObjects.DTOs;

public sealed class CustomerRegistrationRequest
{
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Username may only contain letters, numbers, dots, underscores, and hyphens.")]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[0-9]{12}$", ErrorMessage = "CCCD must be exactly 12 digits.")]
    public string CCCD { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^0[0-9]{9,10}$", ErrorMessage = "Invalid Vietnamese phone format.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Address { get; set; }

    [Required]
    public DateTime DateOfBirth { get; set; }

    [Required]
    [MaxLength(20)]
    public string DriverLicenseNo { get; set; } = string.Empty;
}

public sealed class CustomerProfileUpdateRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[0-9]{12}$", ErrorMessage = "CCCD must be exactly 12 digits.")]
    public string CCCD { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^0[0-9]{9,10}$", ErrorMessage = "Invalid Vietnamese phone format.")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Address { get; set; }

    [Required]
    public DateTime DateOfBirth { get; set; }

    [Required]
    [MaxLength(20)]
    public string DriverLicenseNo { get; set; } = string.Empty;
}
