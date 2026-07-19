using DataAccessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using Services.DTOs;

namespace API.Controllers.OData;

[Authorize(Roles = "Admin,Staff")]
[Route("odata")]
public sealed class RentalContractsController : ODataController
{
    private readonly AppDbContext context;

    public RentalContractsController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet("RentalContracts")]
    [EnableQuery(PageSize = 100, MaxTop = 100)]
    public IQueryable<RentalContractListDto> Get()
        => context.RentalContracts
            .AsNoTracking()
            .Select(rental => new RentalContractListDto
            {
                Id = rental.Id,
                CustomerId = rental.CustomerId,
                MotorcycleId = rental.MotorcycleId,
                CustomerFullName = rental.Customer != null ? rental.Customer.FullName : string.Empty,
                MotorcycleLicensePlate = rental.Motorcycle != null ? rental.Motorcycle.LicensePlate : string.Empty,
                RentalDate = rental.StartDate,
                ExpectedReturnDate = rental.EndDate,
                DailyRate = rental.DailyPrice,
                TotalAmount = rental.TotalAmount,
                DepositAmount = rental.DepositAmount,
                FinalAmount = rental.FinalAmount,
                CancellationFee = rental.CancellationFee,
                Status = (int)rental.Status,
                CreatedBy = rental.CreatedByUser != null
                    ? rental.CreatedByUser.FullName
                    : rental.CreatedBy,
                CreatedAt = rental.CreatedAt
            });
}
