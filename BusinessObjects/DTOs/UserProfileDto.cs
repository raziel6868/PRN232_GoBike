using BusinessObjects.Enums;

namespace BusinessObjects.DTOs;

public class UserProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public UserRole Role { get; set; }
    public string RoleName => Role.ToString();
    public int? CustomerId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Address { get; set; }
    public string? CCCD { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? DriverLicenseNo { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
