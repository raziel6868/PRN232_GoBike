using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs;
using Services.Interfaces;

namespace API.Controllers;

[Authorize(Roles = "Admin,Staff")]
[ApiController]
[Route("api/[controller]")]
public class CustomerController : ControllerBase
{
    private readonly ICustomerService customerService;

    public CustomerController(ICustomerService customerService)
    {
        this.customerService = customerService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Customer>>> GetAll([FromQuery] string? search = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var data = await customerService.GetAllAsync();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim().ToLower();
            data = data.Where(x =>
                x.FullName.ToLower().Contains(search) ||
                x.CCCD.ToLower().Contains(search) ||
                x.PhoneNumber.ToLower().Contains(search)).ToList();
        }

        var result = data
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Customer>> GetById(int id)
    {
        var customer = await customerService.GetByIdAsync(id);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CustomerCreateDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var customer = new Customer
        {
            FullName = dto.FullName,
            CCCD = dto.CCCD,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            Address = dto.Address,
            DateOfBirth = dto.DateOfBirth,
            DriverLicenseNo = dto.DriverLicenseNo,
            IsActive = true,
            CreatedAt = SystemClock.Now
        };

        try
        {
            await customerService.CreateAsync(customer, dto.Username);
            return CreatedAtAction(nameof(GetById), new { id = customer.Id }, new
            {
                customer.Id,
                Username = dto.Username.Trim(),
                CustomerCreateDto.DefaultPassword
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(int id, [FromBody] CustomerUpdateDto dto)
    {
        if (id != dto.Id)
        {
            return BadRequest("Route id and payload id do not match");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var customer = new Customer
        {
            Id = dto.Id,
            FullName = dto.FullName,
            CCCD = dto.CCCD,
            PhoneNumber = dto.PhoneNumber,
            Email = dto.Email,
            Address = dto.Address,
            DateOfBirth = dto.DateOfBirth,
            DriverLicenseNo = dto.DriverLicenseNo,
            IsActive = dto.IsActive
        };

        try
        {
            await customerService.UpdateAsync(customer);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        return await Deactivate(id);
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id}/deactivate")]
    public async Task<ActionResult> Deactivate(int id)
    {
        try
        {
            await customerService.DeactivateAsync(id);
            return Ok(new { message = "Customer deactivated successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id}/reactivate")]
    public async Task<ActionResult> Reactivate(int id)
    {
        try
        {
            await customerService.ReactivateAsync(id);
            return Ok(new { message = "Customer reactivated successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
