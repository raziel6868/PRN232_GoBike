using System.ComponentModel.DataAnnotations;

namespace BusinessObjects.DTOs;

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

public sealed class InternalProfileUpdateRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required]
    [MaxLength(100)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
