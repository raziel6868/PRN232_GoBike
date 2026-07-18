using System.Security.Claims;
using BusinessObjects.Entities;
using BusinessObjects.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs;
using Services.Interfaces;

namespace API.Controllers;

[Authorize(Roles = "Admin,Staff")]
[ApiController]
[Route("api/rental-contracts")]
[Route("api/[controller]")]
public class RentalContractController : ControllerBase
{
    private readonly IRentalContractService rentalContractService;

    public RentalContractController(IRentalContractService rentalContractService)
    {
        this.rentalContractService = rentalContractService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RentalContractListDto>>> GetAll(
        [FromQuery] int? customerId = null,
        [FromQuery] int? motorcycleId = null,
        [FromQuery] RentalStatus? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var data = await rentalContractService.GetAllAsync();

        if (customerId.HasValue)
            data = data.Where(x => x.CustomerId == customerId.Value).ToList();

        if (motorcycleId.HasValue)
            data = data.Where(x => x.MotorcycleId == motorcycleId.Value).ToList();

        if (status.HasValue)
            data = data.Where(x => x.Status == status.Value).ToList();

        if (fromDate.HasValue)
            data = data.Where(x => x.StartDate.Date >= fromDate.Value.Date).ToList();

        if (toDate.HasValue)
            data = data.Where(x => x.StartDate.Date <= toDate.Value.Date).ToList();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = data
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapListDto)
            .ToList();

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<RentalContractDetailDto>> GetById(int id)
    {
        var rental = await rentalContractService.GetByIdAsync(id);
        return rental is null ? NotFound(new { message = "Rental contract not found" }) : Ok(MapDetailDto(rental));
    }

    [HttpPost("reserve")]
    public async Task<ActionResult<RentalContractDetailDto>> Reserve([FromBody] ReserveRentalRequestDto request)
        => await ExecuteWorkflowAsync(async () =>
        {
            var rental = await rentalContractService.ReserveAsync(request, GetUserId());
            return CreatedAtAction(nameof(GetById), new { id = rental.Id }, MapDetailDto(rental));
        });

    [HttpPost("rent-now")]
    public async Task<ActionResult<RentalContractDetailDto>> RentNow([FromBody] RentNowRequestDto request)
        => await ExecuteWorkflowAsync(async () =>
        {
            var rental = await rentalContractService.RentNowAsync(request, GetUserId());
            return CreatedAtAction(nameof(GetById), new { id = rental.Id }, MapDetailDto(rental));
        });

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/handover")]
    public async Task<ActionResult<RentalContractDetailDto>> Handover(int id, [FromBody] HandoverRentalRequestDto request)
        => await ExecuteWorkflowAsync(async () =>
        {
            var rental = await rentalContractService.HandoverAsync(id, request, GetUserId());
            return Ok(MapDetailDto(rental));
        });

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<RentalContractDetailDto>> Complete(int id, [FromBody] CompleteRentalRequestDto request)
        => await ExecuteWorkflowAsync(async () =>
        {
            var rental = await rentalContractService.CompleteAsync(id, request, GetUserId());
            return Ok(MapDetailDto(rental));
        });

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<RentalContractDetailDto>> Cancel(int id, [FromBody] CancelRentalRequestDto request)
        => await ExecuteWorkflowAsync(async () =>
        {
            var rental = await rentalContractService.CancelAsync(id, request, GetUserId());
            return Ok(MapDetailDto(rental));
        });

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/no-show")]
    public async Task<ActionResult<RentalContractDetailDto>> MarkNoShow(int id, [FromBody] NoShowRentalRequestDto request)
        => await ExecuteWorkflowAsync(async () =>
        {
            var rental = await rentalContractService.MarkNoShowAsync(id, request, GetUserId());
            return Ok(MapDetailDto(rental));
        });

    private async Task<ActionResult<RentalContractDetailDto>> ExecuteWorkflowAsync(Func<Task<ActionResult<RentalContractDetailDto>>> action)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            return await action();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private int? GetUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(value, out var id) ? id : null;
    }

    private static RentalContractListDto MapListDto(RentalContract x) => new()
    {
        Id = x.Id,
        CustomerId = x.CustomerId,
        MotorcycleId = x.MotorcycleId,
        CustomerFullName = x.Customer?.FullName,
        MotorcycleLicensePlate = x.Motorcycle?.LicensePlate,
        RentalDate = x.StartDate,
        ExpectedReturnDate = x.EndDate,
        DailyRate = x.DailyPrice,
        TotalAmount = x.TotalAmount,
        DepositAmount = x.DepositAmount,
        FinalAmount = x.FinalAmount,
        CancellationFee = x.CancellationFee,
        Status = (int)x.Status,
        StatusText = x.Status.ToString(),
        CreatedBy = x.CreatedBy
    };

    private static RentalContractDetailDto MapDetailDto(RentalContract rental) => new()
    {
        Id = rental.Id,
        CustomerId = rental.CustomerId,
        MotorcycleId = rental.MotorcycleId,
        CustomerFullName = rental.Customer?.FullName,
        MotorcycleLicensePlate = rental.Motorcycle?.LicensePlate,
        MotorcycleMileage = rental.Motorcycle?.Mileage,
        RentalDate = rental.StartDate,
        ExpectedReturnDate = rental.EndDate,
        ActualReturnDate = rental.ActualReturnDate,
        DailyRate = rental.DailyPrice,
        RentalDays = rental.RentalDays,
        TotalAmount = rental.TotalAmount,
        DepositAmount = rental.DepositAmount,
        LateDays = rental.LateDays,
        LateFee = rental.LateFee,
        DamageFee = rental.DamageFee,
        DamageDescription = rental.DamageDescription,
        OtherFee = rental.OtherFee,
        OtherFeeDescription = rental.OtherFeeDescription,
        DiscountAmount = rental.DiscountAmount,
        DiscountReason = rental.DiscountReason,
        FinalAmount = rental.FinalAmount,
        RemainingAmount = rental.RemainingAmount,
        AdditionalPaymentAmount = rental.AdditionalPaymentAmount,
        RefundAmount = rental.RefundAmount,
        CancellationFee = rental.CancellationFee,
        Status = (int)rental.Status,
        StatusText = rental.Status.ToString(),
        StartMileage = rental.Inspections.FirstOrDefault(i => i.InspectionType == InspectionType.BeforeRental)?.Mileage,
        EndMileage = rental.Inspections.FirstOrDefault(i => i.InspectionType == InspectionType.AfterReturn)?.Mileage,
        Notes = rental.Notes,
        CreatedBy = rental.CreatedBy,
        CompletedAt = rental.CompletedAt,
        CancelledAt = rental.CancelledAt,
        NoShowAt = rental.NoShowAt,
        CancellationReason = rental.CancellationReason,
        NoShowReason = rental.NoShowReason,
        Inspections = rental.Inspections
            .OrderBy(i => i.CreatedAt)
            .Select(i => new RentalInspectionDto
            {
                Id = i.Id,
                InspectionType = i.InspectionType.ToString(),
                Mileage = i.Mileage,
                FuelLevel = i.FuelLevel,
                VehicleCondition = i.VehicleCondition,
                HasDamage = i.HasDamage,
                DamageDescription = i.DamageDescription,
                AccessoriesNote = i.AccessoriesNote,
                ImageUrl = i.ImageUrl,
                Note = i.Note,
                CreatedAt = i.CreatedAt
            }).ToList(),
        Payments = rental.Payments
            .OrderBy(p => p.CreatedAt)
            .Select(p => new RentalPaymentDto
            {
                Id = p.Id,
                PaymentType = p.PaymentType.ToString(),
                Amount = p.Amount,
                PaymentMethod = p.PaymentMethod.ToString(),
                Note = p.Note,
                ProofImageUrl = p.ProofImageUrl,
                CreatedAt = p.CreatedAt
            }).ToList()
    };
}
