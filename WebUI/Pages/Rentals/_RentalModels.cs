using BusinessObjects;

namespace WebUI.Pages.Rentals;

public class RentalListItem
{
    public int Id { get; set; }
    public string? CustomerFullName { get; set; }
    public string? MotorcycleLicensePlate { get; set; }
    public DateTime RentalDate { get; set; }
    public DateTime ExpectedReturnDate { get; set; }
    public decimal DailyRate { get; set; }
    public decimal TotalAmount { get; set; }
    public int Status { get; set; }
    public string? CreatedBy { get; set; }
}

public class RentalDetail
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int MotorcycleId { get; set; }
    public DateTime RentalDate { get; set; }
    public DateTime ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public int? MotorcycleMileage { get; set; }
    public decimal DailyRate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public int Status { get; set; }
    public int? StartMileage { get; set; }
    public int? EndMileage { get; set; }
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public string? CustomerFullName { get; set; }
    public string? MotorcycleLicensePlate { get; set; }
    public List<RentalInspectionView> Inspections { get; set; } = [];
    public List<RentalPaymentView> Payments { get; set; } = [];
}

public class RentalInspectionView
{
    public int Id { get; set; }
    public string InspectionType { get; set; } = "";
    public int Mileage { get; set; }
    public string FuelLevel { get; set; } = "";
    public string VehicleCondition { get; set; } = "";
    public bool HasDamage { get; set; }
    public string? DamageDescription { get; set; }
    public string? AccessoriesNote { get; set; }
    public string? ImageUrl { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RentalPaymentView
{
    public int Id { get; set; }
    public string PaymentType { get; set; } = "";
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "";
    public string? Note { get; set; }
    public string? ProofImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CustomerOption
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
}

public class MotorcycleOption
{
    public int Id { get; set; }
    public string LicensePlate { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public decimal DailyRate { get; set; }
    public int Mileage { get; set; }
}

public class AvailableMotorcycle
{
    public int Id { get; set; }
    public string LicensePlate { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public decimal DailyRate { get; set; }
    public int Mileage { get; set; }
}

public class RentalCreateForm
{
    public int CustomerId { get; set; }
    public int MotorcycleId { get; set; }
    public DateTime RentalDate { get; set; } = SystemClock.Today;
    public DateTime ExpectedReturnDate { get; set; } = SystemClock.Today.AddDays(1);
    public bool DepositConfirmed { get; set; }
    public int DepositPaymentMethod { get; set; } = 1;
    public string? DepositPaymentNote { get; set; }
    public string? Notes { get; set; }
}

public class RentalRentNowForm : RentalCreateForm
{
    public int StartMileage { get; set; }
    public string FuelLevel { get; set; } = "Full";
    public string VehicleCondition { get; set; } = "Good";
    public bool HasDamage { get; set; }
    public string? DamageDescription { get; set; }
    public string? AccessoriesNote { get; set; }
    public string? InspectionNote { get; set; }
}

public class RentalCreatedResult
{
    public int Id { get; set; }
}
