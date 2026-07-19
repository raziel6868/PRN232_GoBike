using System.Security.Claims;
using BusinessObjects.Enums;
using DataAccessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.DTOs;

namespace API.Controllers;

[Authorize(Roles = "Customer")]
[ApiController]
[Route("api/my-rental-contracts")]
public sealed class MyRentalContractsController : ControllerBase
{
    private readonly AppDbContext context;

    public MyRentalContractsController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MyRentalContractDto>>> GetMine(
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();

        var customerId = await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId &&
                           user.Role == UserRole.Customer &&
                           user.IsActive &&
                           user.Customer != null &&
                           user.Customer.IsActive)
            .Select(user => user.CustomerId)
            .SingleOrDefaultAsync(cancellationToken);

        if (!customerId.HasValue)
            return Forbid();

        var contracts = await context.RentalContracts
            .AsNoTracking()
            .Where(contract => contract.CustomerId == customerId.Value)
            .OrderByDescending(contract => contract.CreatedAt)
            .Select(contract => new MyRentalContractDto
            {
                Id = contract.Id,
                MotorcycleLicensePlate = contract.Motorcycle != null
                    ? contract.Motorcycle.LicensePlate
                    : string.Empty,
                MotorcycleName = contract.Motorcycle != null
                    ? contract.Motorcycle.Brand + " " + contract.Motorcycle.Model
                    : string.Empty,
                RentalDate = contract.StartDate,
                ExpectedReturnDate = contract.EndDate,
                ActualReturnDate = contract.ActualReturnDate,
                DepositAmount = contract.DepositAmount,
                TotalAmount = contract.TotalAmount,
                FinalAmount = contract.FinalAmount,
                Status = (int)contract.Status,
                CreatedAt = contract.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return Ok(contracts);
    }
}
