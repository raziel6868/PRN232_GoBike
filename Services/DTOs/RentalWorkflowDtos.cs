using System.ComponentModel.DataAnnotations;
using BusinessObjects.Enums;

namespace Services.DTOs;

public class RentalInspectionRequestDto
{
    [Range(0, int.MaxValue)]
    public int Mileage { get; set; }

    [Required]
    [MaxLength(50)]
    public string FuelLevel { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string VehicleCondition { get; set; } = string.Empty;

    public bool HasDamage { get; set; }

    [MaxLength(500)]
    public string? DamageDescription { get; set; }

    [MaxLength(500)]
    public string? AccessoriesNote { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class ReserveRentalRequestDto
{
    [Required]
    public int CustomerId { get; set; }

    [Required]
    public int MotorcycleId { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public bool DepositConfirmed { get; set; }

    public PaymentMethod DepositPaymentMethod { get; set; }

    [MaxLength(500)]
    public string? DepositPaymentNote { get; set; }

    [Required]
    [MaxLength(500)]
    public string DepositProofImageUrl { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; set; }
}

public class RentNowRequestDto : ReserveRentalRequestDto
{
    [Required]
    public RentalInspectionRequestDto BeforeInspection { get; set; } = new();
}

public class HandoverRentalRequestDto
{
    [Required]
    public RentalInspectionRequestDto BeforeInspection { get; set; } = new();
}

public class CompleteRentalRequestDto
{
    [Required]
    public DateTime ActualReturnDate { get; set; }

    [Required]
    public RentalInspectionRequestDto AfterInspection { get; set; } = new();

    [Range(0, double.MaxValue)]
    public decimal DamageFee { get; set; }

    [MaxLength(500)]
    public string? DamageDescription { get; set; }

    [Range(0, double.MaxValue)]
    public decimal OtherFee { get; set; }

    [MaxLength(500)]
    public string? OtherFeeDescription { get; set; }

    [Range(0, double.MaxValue)]
    public decimal DiscountAmount { get; set; }

    [MaxLength(500)]
    public string? DiscountReason { get; set; }

    public MotorcycleStatus MotorcycleStatusAfterReturn { get; set; } = MotorcycleStatus.Available;

    public PaymentMethod? SettlementPaymentMethod { get; set; }

    [MaxLength(500)]
    public string? SettlementPaymentNote { get; set; }
}

public class CancelRentalRequestDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public PaymentMethod RefundPaymentMethod { get; set; }

    [MaxLength(500)]
    public string? RefundPaymentNote { get; set; }
}

public class NoShowRentalRequestDto
{
    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;
}
