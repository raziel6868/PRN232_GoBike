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
public sealed class MotorcyclesController : ODataController
{
    private readonly AppDbContext context;

    public MotorcyclesController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet("Motorcycles")]
    [EnableQuery(PageSize = 100, MaxTop = 100)]
    public IQueryable<MotorcycleDto> Get()
        => context.Motorcycles
            .AsNoTracking()
            .Where(motorcycle => motorcycle.IsActive)
            .Select(motorcycle => new MotorcycleDto
            {
                Id = motorcycle.Id,
                LicensePlate = motorcycle.LicensePlate,
                Brand = motorcycle.Brand,
                Model = motorcycle.Model,
                VehicleTypeId = motorcycle.VehicleTypeId,
                VehicleTypeName = motorcycle.VehicleType != null ? motorcycle.VehicleType.Name : string.Empty,
                Status = motorcycle.Status,
                StatusCode = (int)motorcycle.Status,
                DailyRate = motorcycle.VehicleType != null ? motorcycle.VehicleType.DefaultDailyRate : 0,
                Color = motorcycle.Color,
                Mileage = motorcycle.Mileage,
                RegistrationNo = motorcycle.RegistrationNo,
                ImageUrl = motorcycle.ImageUrl,
                CreatedAt = motorcycle.CreatedAt,
                UpdatedAt = motorcycle.UpdatedAt
            });
}
