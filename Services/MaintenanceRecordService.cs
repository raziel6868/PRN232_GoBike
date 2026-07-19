using BusinessObjects;
using BusinessObjects.Entities;
using BusinessObjects.Enums;
using DataAccessObjects;
using Microsoft.EntityFrameworkCore;
using Services.DTOs;
using Services.Interfaces;

namespace Services;

public class MaintenanceRecordService : IMaintenanceRecordService
{
    private readonly AppDbContext context;

    public MaintenanceRecordService(AppDbContext context)
    {
        this.context = context;
    }

    public async Task<PaginatedResult<MaintenanceRecordDto>> GetAllAsync(
        int? motorcycleId,
        MaintenanceStatus? status,
        int page,
        int pageSize)
    {
        var query = context.MaintenanceRecords
            .Include(x => x.Motorcycle)
            .AsQueryable();

        if (motorcycleId.HasValue)
        {
            query = query.Where(x => x.MotorcycleId == motorcycleId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PaginatedResult<MaintenanceRecordDto>
        {
            Items = items.Select(MapToDto).ToList(),
            CurrentPage = page,
            PageSize = pageSize,
            TotalItems = total
        };
    }

    public async Task<MaintenanceRecordDto?> GetByIdAsync(int id)
    {
        var record = await context.MaintenanceRecords
            .Include(x => x.Motorcycle)
            .FirstOrDefaultAsync(x => x.Id == id);

        return record == null ? null : MapToDto(record);
    }

    public async Task<MaintenanceRecordDto> CreateAsync(MaintenanceRecordCreateDto dto, int? userId)
    {
        ValidateEditableStatus(dto.Status);
        ValidateCommon(dto.Reason, dto.RepairCost);

        var motorcycle = await context.Motorcycles.FirstOrDefaultAsync(x => x.Id == dto.MotorcycleId)
            ?? throw new InvalidOperationException($"Motorcycle with ID {dto.MotorcycleId} not found.");

        if (!motorcycle.IsActive)
        {
            throw new InvalidOperationException("Cannot create maintenance record for inactive motorcycle.");
        }

        if (motorcycle.Status is MotorcycleStatus.Rented or MotorcycleStatus.Reserved)
        {
            throw new InvalidOperationException("Cannot manually create maintenance record for rented or reserved motorcycle.");
        }

        if (dto.RentalContractId.HasValue)
        {
            var contractExists = await context.RentalContracts.AnyAsync(x =>
                x.Id == dto.RentalContractId.Value && x.MotorcycleId == dto.MotorcycleId);
            if (!contractExists)
            {
                throw new InvalidOperationException("Rental contract not found or does not belong to this motorcycle.");
            }
        }

        var startDate = dto.StartDate?.Date ?? SystemClock.Today;
        var record = new MaintenanceRecord
        {
            MotorcycleId = dto.MotorcycleId,
            RentalContractId = dto.RentalContractId,
            Reason = dto.Reason.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            RepairCost = dto.RepairCost,
            Status = dto.Status,
            StartDate = startDate,
            CreatedByUserId = userId,
            CreatedAt = SystemClock.Now
        };

        motorcycle.Status = MotorcycleStatus.Maintenance;
        motorcycle.UpdatedAt = SystemClock.Now;

        context.MaintenanceRecords.Add(record);
        await context.SaveChangesAsync();

        record.Motorcycle = motorcycle;
        return MapToDto(record);
    }

    public async Task<MaintenanceRecordDto> UpdateAsync(int id, MaintenanceRecordUpdateDto dto, int? userId)
    {
        ValidateEditableStatus(dto.Status);
        ValidateCommon(dto.Reason, dto.RepairCost);

        var record = await LoadRecordAsync(id);
        if (record.Status is MaintenanceStatus.Completed or MaintenanceStatus.Cancelled)
        {
            throw new InvalidOperationException("Cannot update a completed or cancelled maintenance record.");
        }

        record.Reason = dto.Reason.Trim();
        record.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        record.RepairCost = dto.RepairCost;
        record.Status = dto.Status;
        record.StartDate = dto.StartDate?.Date ?? record.StartDate;
        record.UpdatedByUserId = userId;
        record.UpdatedAt = SystemClock.Now;

        if (record.Motorcycle != null)
        {
            record.Motorcycle.Status = MotorcycleStatus.Maintenance;
            record.Motorcycle.UpdatedAt = SystemClock.Now;
        }

        await context.SaveChangesAsync();
        return MapToDto(record);
    }

    public async Task<MaintenanceRecordDto> StartAsync(int id, int? userId)
    {
        var record = await LoadRecordAsync(id);
        if (record.Status != MaintenanceStatus.Pending)
        {
            throw new InvalidOperationException("Only pending maintenance records can be started.");
        }

        record.Status = MaintenanceStatus.InProgress;
        record.UpdatedByUserId = userId;
        record.UpdatedAt = SystemClock.Now;

        if (record.Motorcycle != null)
        {
            record.Motorcycle.Status = MotorcycleStatus.Maintenance;
            record.Motorcycle.UpdatedAt = SystemClock.Now;
        }

        await context.SaveChangesAsync();
        return MapToDto(record);
    }

    public async Task<MaintenanceRecordDto> CompleteAsync(int id, MaintenanceCompleteDto dto, int? userId)
    {
        var record = await LoadRecordAsync(id);
        if (record.Status != MaintenanceStatus.InProgress)
        {
            throw new InvalidOperationException("Only in-progress maintenance records can be completed.");
        }

        var endDate = dto.EndDate?.Date ?? SystemClock.Today;
        if (endDate < record.StartDate.Date)
        {
            throw new InvalidOperationException("EndDate cannot be earlier than StartDate.");
        }

        record.Status = MaintenanceStatus.Completed;
        record.EndDate = endDate;
        record.UpdatedByUserId = userId;
        record.UpdatedAt = SystemClock.Now;

        if (!await HasOtherOpenMaintenanceAsync(record.MotorcycleId, record.Id) && record.Motorcycle != null)
        {
            record.Motorcycle.Status = MotorcycleStatus.Available;
            record.Motorcycle.UpdatedAt = SystemClock.Now;
        }

        await context.SaveChangesAsync();
        return MapToDto(record);
    }

    public async Task<MaintenanceRecordDto> CancelAsync(int id, int? userId)
    {
        var record = await LoadRecordAsync(id);
        if (record.Status == MaintenanceStatus.Completed)
        {
            throw new InvalidOperationException("Cannot cancel a completed maintenance record.");
        }

        record.Status = MaintenanceStatus.Cancelled;
        record.UpdatedByUserId = userId;
        record.UpdatedAt = SystemClock.Now;

        if (!await HasOtherOpenMaintenanceAsync(record.MotorcycleId, record.Id) && record.Motorcycle != null)
        {
            record.Motorcycle.Status = MotorcycleStatus.Available;
            record.Motorcycle.UpdatedAt = SystemClock.Now;
        }

        await context.SaveChangesAsync();
        return MapToDto(record);
    }

    private async Task<MaintenanceRecord> LoadRecordAsync(int id)
        => await context.MaintenanceRecords
            .Include(x => x.Motorcycle)
            .FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new KeyNotFoundException($"Maintenance record with ID {id} not found.");

    private Task<bool> HasOtherOpenMaintenanceAsync(int motorcycleId, int currentRecordId)
        => context.MaintenanceRecords.AnyAsync(x =>
            x.MotorcycleId == motorcycleId &&
            x.Id != currentRecordId &&
            (x.Status == MaintenanceStatus.Pending || x.Status == MaintenanceStatus.InProgress));

    private static void ValidateEditableStatus(MaintenanceStatus status)
    {
        if (status is MaintenanceStatus.Completed or MaintenanceStatus.Cancelled)
        {
            throw new InvalidOperationException("Use complete or cancel endpoint to close a maintenance record.");
        }
    }

    private static void ValidateCommon(string reason, decimal repairCost)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Reason is required.");
        }

        if (repairCost < 0)
        {
            throw new InvalidOperationException("Repair cost cannot be negative.");
        }
    }

    private static MaintenanceRecordDto MapToDto(MaintenanceRecord record) => new()
    {
        Id = record.Id,
        MotorcycleId = record.MotorcycleId,
        MotorcycleLicensePlate = record.Motorcycle?.LicensePlate,
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
    };
}
