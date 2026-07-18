using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessObjects.Entities;
using Services.DTOs;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MotorcycleTypesController : ControllerBase
{
    private readonly IMotorcycleTypeService _service;

    public MotorcycleTypesController(IMotorcycleTypeService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var types = await _service.GetAllAsync();
        var dtos = types.Select(MapToDto).ToList();
        return Ok(dtos);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var type = await _service.GetByIdAsync(id);
        return type is null ? NotFound(new { message = $"Motorcycle type with ID {id} not found." }) : Ok(MapToDto(type));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MotorcycleTypeUpsertDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var type = await _service.CreateAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = type.Id }, MapToDto(type));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] MotorcycleTypeUpsertDto dto)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var type = await _service.UpdateAsync(id, dto);
            return Ok(MapToDto(type));
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
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        return await Deactivate(id);
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        try
        {
            await _service.DeactivateAsync(id);
            return Ok(new { message = "Motorcycle type deactivated successfully." });
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
    [HttpPatch("{id}/reactivate")]
    public async Task<IActionResult> Reactivate(int id)
    {
        try
        {
            await _service.ReactivateAsync(id);
            return Ok(new { message = "Motorcycle type reactivated successfully." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private static MotorcycleTypeDto MapToDto(MotorcycleType type) => new()
    {
        Id = type.Id,
        Name = type.Name,
        Description = type.Description,
        DefaultDailyRate = type.DefaultDailyRate,
        DefaultDepositAmount = type.DefaultDepositAmount,
        IsActive = type.IsActive,
        CreatedAt = type.CreatedAt,
        UpdatedAt = type.UpdatedAt
    };
}
