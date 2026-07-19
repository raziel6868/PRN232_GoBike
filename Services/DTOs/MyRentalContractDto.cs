namespace Services.DTOs;

public sealed class MyRentalContractDto
{
    public int Id { get; set; }
    public string MotorcycleLicensePlate { get; set; } = string.Empty;
    public string MotorcycleName { get; set; } = string.Empty;
    public DateTime RentalDate { get; set; }
    public DateTime ExpectedReturnDate { get; set; }
    public DateTime? ActualReturnDate { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public int Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
