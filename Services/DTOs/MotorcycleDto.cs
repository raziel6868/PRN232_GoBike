using BusinessObjects.Enums;

namespace Services.DTOs;

public class MotorcycleDto
{
    public int Id { get; set; }
    public string LicensePlate { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int VehicleTypeId { get; set; }
    public string VehicleTypeName { get; set; } = string.Empty;
    public MotorcycleStatus Status { get; set; }
    public int StatusCode { get; set; }
    public string StatusText => Status.ToString();
    public decimal DailyRate { get; set; }
    public string Color { get; set; } = string.Empty;
    public int Mileage { get; set; }
    public string? RegistrationNo { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MotorcycleDetailDto : MotorcycleDto
{
    public bool IsActive { get; set; }
    public List<RentalHistoryItem> RecentRentals { get; set; } = [];
}

public class RentalHistoryItem
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime RentalDate { get; set; }
    public DateTime ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public RentalStatus Status { get; set; }
    public string StatusText => Status.ToString();
    public decimal TotalAmount { get; set; }
}
