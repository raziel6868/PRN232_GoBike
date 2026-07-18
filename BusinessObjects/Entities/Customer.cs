using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BusinessObjects;
using BusinessObjects.Enums;

namespace BusinessObjects.Entities;

public class Customer
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [MaxLength(12)]
    public string CCCD { get; set; } = string.Empty;

    [Required]
    [MaxLength(15)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(255)]
    public string? Address { get; set; }

    [Required]
    public DateTime DateOfBirth { get; set; }

    [NotMapped]
    public int Age => SystemClock.Today.Year - DateOfBirth.Year -
        (DateOfBirth.Date > SystemClock.Today.AddYears(-(SystemClock.Today.Year - DateOfBirth.Year)) ? 1 : 0);

    [Required]
    [MaxLength(20)]
    public string DriverLicenseNo { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = SystemClock.Now;

    public DateTime? UpdatedAt { get; set; }

    public User? User { get; set; }

    public ICollection<RentalContract> RentalContracts { get; set; } = new List<RentalContract>();
}
