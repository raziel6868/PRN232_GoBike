namespace Services.DTOs;

public class RentalContractListDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int MotorcycleId { get; set; }
    public string? CustomerFullName { get; set; }
    public string? MotorcycleLicensePlate { get; set; }
    public DateTime RentalDate { get; set; }
    public DateTime ExpectedReturnDate { get; set; }
    public decimal DailyRate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal CancellationFee { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RentalContractDetailDto
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int MotorcycleId { get; set; }
    public string? CustomerFullName { get; set; }
    public string? MotorcycleLicensePlate { get; set; }
    public int? MotorcycleMileage { get; set; }
    public DateTime RentalDate { get; set; }
    public DateTime ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public decimal DailyRate { get; set; }
    public int RentalDays { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public int LateDays { get; set; }
    public decimal LateFee { get; set; }
    public decimal DamageFee { get; set; }
    public string? DamageDescription { get; set; }
    public decimal OtherFee { get; set; }
    public string? OtherFeeDescription { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal AdditionalPaymentAmount { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal CancellationFee { get; set; }
    public int Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public int? StartMileage { get; set; }
    public int? EndMileage { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? NoShowAt { get; set; }
    public string? CancellationReason { get; set; }
    public string? NoShowReason { get; set; }
    public List<RentalInspectionDto> Inspections { get; set; } = [];
    public List<RentalPaymentDto> Payments { get; set; } = [];
}

public class RentalInspectionDto
{
    public int Id { get; set; }
    public string InspectionType { get; set; } = string.Empty;
    public int Mileage { get; set; }
    public string FuelLevel { get; set; } = string.Empty;
    public string VehicleCondition { get; set; } = string.Empty;
    public bool HasDamage { get; set; }
    public string? DamageDescription { get; set; }
    public string? AccessoriesNote { get; set; }
    public string? ImageUrl { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RentalPaymentDto
{
    public int Id { get; set; }
    public string PaymentType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? ProofImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
