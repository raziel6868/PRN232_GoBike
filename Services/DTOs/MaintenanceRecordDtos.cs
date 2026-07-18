using System.ComponentModel.DataAnnotations;
using BusinessObjects.Enums;

namespace Services.DTOs;

public class MaintenanceRecordDto
{
    public int Id { get; set; }
    public int MotorcycleId { get; set; }
    public string? MotorcycleLicensePlate { get; set; }
    public int? RentalContractId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal RepairCost { get; set; }
    public MaintenanceStatus Status { get; set; }
    public int StatusCode { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MaintenanceRecordCreateDto
{
    [Required]
    public int MotorcycleId { get; set; }

    public int? RentalContractId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Repair cost cannot be negative")]
    public decimal RepairCost { get; set; }

    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Pending;

    public DateTime? StartDate { get; set; }
}

public class MaintenanceRecordUpdateDto
{
    [Required]
    [MaxLength(200)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Repair cost cannot be negative")]
    public decimal RepairCost { get; set; }

    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.Pending;

    public DateTime? StartDate { get; set; }
}

public class MaintenanceCompleteDto
{
    public DateTime? EndDate { get; set; }
}
