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
public sealed class CustomersController : ODataController
{
    private readonly AppDbContext context;

    public CustomersController(AppDbContext context)
    {
        this.context = context;
    }

    [HttpGet("Customers")]
    [EnableQuery(PageSize = 100, MaxTop = 100)]
    public IQueryable<CustomerListDto> Get()
        => context.Customers
            .AsNoTracking()
            .Select(customer => new CustomerListDto
            {
                Id = customer.Id,
                FullName = customer.FullName,
                CCCD = customer.CCCD,
                PhoneNumber = customer.PhoneNumber,
                Email = customer.Email,
                DateOfBirth = customer.DateOfBirth,
                IsActive = customer.IsActive,
                CreatedAt = customer.CreatedAt
            });
}
