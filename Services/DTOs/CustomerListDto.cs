namespace Services.DTOs;

public class CustomerListDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string CCCD { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime DateOfBirth { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}
