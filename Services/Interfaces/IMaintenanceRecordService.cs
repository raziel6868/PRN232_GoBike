using BusinessObjects.Enums;
using Services.DTOs;

namespace Services.Interfaces;

public interface IMaintenanceRecordService
{
    Task<PaginatedResult<MaintenanceRecordDto>> GetAllAsync(int? motorcycleId, MaintenanceStatus? status, int page, int pageSize);
    Task<MaintenanceRecordDto?> GetByIdAsync(int id);
    Task<MaintenanceRecordDto> CreateAsync(MaintenanceRecordCreateDto dto, int? userId);
    Task<MaintenanceRecordDto> UpdateAsync(int id, MaintenanceRecordUpdateDto dto, int? userId);
    Task<MaintenanceRecordDto> StartAsync(int id, int? userId);
    Task<MaintenanceRecordDto> CompleteAsync(int id, MaintenanceCompleteDto dto, int? userId);
    Task<MaintenanceRecordDto> CancelAsync(int id, int? userId);
}
