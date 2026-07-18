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
public sealed class MaintenanceRecordsController : ODataController
{
    private readonly AppDbContext context;

    public MaintenanceRecordsController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet("MaintenanceRecords")]
    [EnableQuery(PageSize = 100, MaxTop = 100)]
    public IQueryable<MaintenanceRecordDto> Get()
        => context.MaintenanceRecords
            .AsNoTracking()
            .Select(record => new MaintenanceRecordDto
            {
                Id = record.Id,
                MotorcycleId = record.MotorcycleId,
                MotorcycleLicensePlate = record.Motorcycle != null ? record.Motorcycle.LicensePlate : string.Empty,
                RentalContractId = record.RentalContractId,
                Reason = record.Reason,
                Description = record.Description,
                RepairCost = record.RepairCost,
                Status = record.Status,
                StatusCode = (int)record.Status,
                StartDate = record.StartDate,
                EndDate = record.EndDate,
                CreatedByUserId = record.CreatedByUserId,
                CreatedAt = record.CreatedAt,
                UpdatedByUserId = record.UpdatedByUserId,
                UpdatedAt = record.UpdatedAt
            });
}
