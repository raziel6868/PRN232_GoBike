using BusinessObjects.DTOs;
using BusinessObjects.Enums;
using Services.DTOs;

namespace WebUI.Services;

public interface IGoBikeApiClient
{
    Task<(bool Success, LoginResponse? User, string? Error)> LoginAsync(LoginRequest request);
    Task LogoutAsync();
    Task<(bool Success, UserProfileDto? Profile, string? Error)> GetProfileAsync();
    Task<(bool Success, UserProfileDto? Profile, string? Error)> UpdateOwnProfileAsync(
        CustomerProfileUpdateRequest request);
    Task<(bool Success, UserProfileDto? Profile, string? Error)> UpdateInternalProfileAsync(
        InternalProfileUpdateRequest request);
    Task<(bool Success, string? Error)> ChangePasswordAsync(ChangePasswordRequest request);
    Task<(bool Success, List<UserDto>? Users, string? Error)> GetStaffUsersAsync();
    Task<(bool Success, UserDto? User, string? Error)> GetStaffUserAsync(int id);
    Task<(bool Success, UserDto? User, string? Error)> CreateStaffUserAsync(CreateStaffUserRequest request);
    Task<(bool Success, UserDto? User, string? Error)> UpdateStaffUserAsync(int id, UpdateStaffUserRequest request);
    Task<(bool Success, string? Error)> DeleteStaffUserAsync(int id);

    Task<(bool Success, PaginatedResult<MotorcycleDto>? Result, string? Error)> GetMotorcyclesAsync(
        string? search,
        MotorcycleStatus? status,
        decimal? minPrice,
        decimal? maxPrice,
        int pageNumber,
        int pageSize = 20);

    Task<(bool Success, MotorcycleDetailDto? Motorcycle, string? Error)> GetMotorcycleAsync(int id);
    Task<(bool Success, MotorcycleDto? Motorcycle, string? Error)> CreateMotorcycleAsync(CreateMotorcycleRequest request);
    Task<(bool Success, MotorcycleDto? Motorcycle, string? Error)> UpdateMotorcycleAsync(int id, UpdateMotorcycleRequest request);
    Task<(bool Success, string? Error)> DeleteMotorcycleAsync(int id);
    Task<(bool Success, List<MotorcycleTypeDto>? Types, string? Error)> GetMotorcycleTypesAsync();

    Task<(bool Success, PaginatedResult<MaintenanceRecordDto>? Result, string? Error)> GetMaintenanceRecordsAsync(
        int? motorcycleId,
        MaintenanceStatus? status,
        int pageNumber,
        int pageSize = 10);
    Task<(bool Success, MaintenanceRecordDto? Record, string? Error)> CreateMaintenanceRecordAsync(
        MaintenanceRecordCreateDto request);

    Task<(bool Success, List<PlaceSuggestionDto>? Places, string? Error)> SearchPlacesAsync(string query);
    Task<(bool Success, RouteAssistantResponseDto? Response, string? Error)> AskRouteAssistantAsync(
        RouteAssistantRequestDto request);
    Task<(bool Success, RouteResultDto? Route, string? Error)> ComputeRouteAsync(ComputeRouteRequestDto request);
}
