using BusinessObjects.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MotorcyclesController : ControllerBase
{
    private readonly IMotorcycleService _service;

    public MotorcyclesController(IMotorcycleService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] MotorcycleStatus? status = null,
        [FromQuery] decimal? minPrice = null,
        [FromQuery] decimal? maxPrice = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await _service.GetPaginatedAsync(search, status, minPrice, maxPrice, page, pageSize);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var motorcycle = await _service.GetDetailAsync(id);
        if (motorcycle == null)
            return NotFound(new { message = $"Motorcycle with ID {id} not found." });

        if (!User.IsInRole("Admin") && !User.IsInRole("Staff"))
        {
            if (!motorcycle.IsActive)
                return NotFound(new { message = $"Motorcycle with ID {id} not found." });

            motorcycle.RecentRentals = [];
        }

        return Ok(motorcycle);
    }

    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable()
    {
        var motorcycles = await _service.GetAvailableAsync();
        return Ok(motorcycles);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateMotorcycleRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var motorcycle = await _service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = motorcycle.Id }, motorcycle);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin,Staff")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMotorcycleRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            var userRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Staff";
            var motorcycle = await _service.UpdateAsync(id, request, userRole);
            return Ok(motorcycle);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            await _service.SoftDeleteAsync(id);
            return Ok(new { message = "Motorcycle deleted successfully." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        if (request.Status != MotorcycleStatus.Available && request.Status != MotorcycleStatus.Maintenance)
            return BadRequest(new { message = "Can only manually switch between Available and Maintenance." });

        try
        {
            await _service.UpdateStatusAsync(id, request.Status);
            return Ok(new { message = "Status updated successfully." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }
}

public class UpdateStatusRequest
{
    public MotorcycleStatus Status { get; set; }
}
