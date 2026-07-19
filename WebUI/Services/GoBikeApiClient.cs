using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BusinessObjects.DTOs;
using BusinessObjects.Enums;
using Microsoft.Extensions.Options;
using Services.DTOs;
using WebUI.Configuration;
using WebUI.Services.Internal;

namespace WebUI.Services;

public class GoBikeApiClient : IGoBikeApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    private readonly HttpClient httpClient;
    private readonly IApiCookieAccessor cookieAccessor;
    private readonly ApiSettings apiSettings;

    public GoBikeApiClient(
        HttpClient httpClient,
        IApiCookieAccessor cookieAccessor,
        IOptions<ApiSettings> apiSettings)
    {
        this.httpClient = httpClient;
        this.cookieAccessor = cookieAccessor;
        this.apiSettings = apiSettings.Value;
    }

    public async Task<(bool Success, LoginResponse? User, string? Error)> LoginAsync(LoginRequest request)
    {
        cookieAccessor.Clear();

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync("api/auth/login", request, JsonOptions);
        }
        catch (HttpRequestException)
        {
            return (false, null, $"Không kết nối được API tại {apiSettings.BaseUrl}. Hãy chạy project API trước, hoặc chỉnh ApiSettings:BaseUrl theo đúng port API đang chạy.");
        }
        catch (TaskCanceledException)
        {
            return (false, null, $"API tại {apiSettings.BaseUrl} phản hồi quá lâu. Hãy kiểm tra project API có đang chạy ổn không.");
        }

        if (!response.IsSuccessStatusCode)
            return (false, null, await ApiResponseReader.ReadErrorMessageAsync(response));

        var cookieHeader = ReadResponseCookieHeader(response);
        if (string.IsNullOrWhiteSpace(cookieHeader))
            return (false, null, "API login succeeded but did not return an authentication cookie.");

        cookieAccessor.SetCookieHeader(cookieHeader);

        var result = await response.Content.ReadFromJsonAsync<ApiLoginResult>(JsonOptions);
        if (result?.User == null)
            return (false, null, "Invalid response from API");

        return (true, result.User, null);
    }

    private static string? ReadResponseCookieHeader(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
            return null;

        var cookiePairs = setCookieHeaders
            .Select(header => header.Split(';', 2)[0].Trim())
            .Where(cookie =>
            {
                var separatorIndex = cookie.IndexOf('=');
                return separatorIndex > 0 && separatorIndex < cookie.Length - 1;
            })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return cookiePairs.Length == 0 ? null : string.Join("; ", cookiePairs);
    }

    public async Task LogoutAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(cookieAccessor.GetCookieHeader()))
                await httpClient.PostAsync("api/auth/logout", null);
        }
        finally
        {
            cookieAccessor.Clear();
        }
    }

    public async Task<(bool Success, UserProfileDto? Profile, string? Error)> GetProfileAsync()
    {
        var response = await httpClient.GetAsync("api/auth/profile");
        return await ReadAsync<UserProfileDto>(response);
    }

    public async Task<(bool Success, UserProfileDto? Profile, string? Error)> UpdateOwnProfileAsync(
        CustomerProfileUpdateRequest request)
    {
        var response = await httpClient.PutAsJsonAsync("api/auth/profile", request, JsonOptions);
        return await ReadAsync<UserProfileDto>(response);
    }

    public async Task<(bool Success, UserProfileDto? Profile, string? Error)> UpdateInternalProfileAsync(
        InternalProfileUpdateRequest request)
    {
        var response = await httpClient.PutAsJsonAsync("api/auth/profile/internal", request, JsonOptions);
        return await ReadAsync<UserProfileDto>(response);
    }

    public async Task<(bool Success, string? Error)> ChangePasswordAsync(ChangePasswordRequest request)
    {
        var response = await httpClient.PutAsJsonAsync("api/auth/profile/password", request, JsonOptions);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return (false, "Phiên API đã hết hạn. Vui lòng đăng nhập lại.");

        return response.IsSuccessStatusCode
            ? (true, null)
            : (false, await ApiResponseReader.ReadErrorMessageAsync(response));
    }

    public async Task<(bool Success, List<UserDto>? Users, string? Error)> GetStaffUsersAsync()
    {
        var response = await httpClient.GetAsync("api/users/staff");
        return await ReadAsync<List<UserDto>>(response);
    }

    public async Task<(bool Success, UserDto? User, string? Error)> GetStaffUserAsync(int id)
    {
        var response = await httpClient.GetAsync($"api/users/staff/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return (false, null, "Staff user not found");
        return await ReadAsync<UserDto>(response);
    }

    public async Task<(bool Success, UserDto? User, string? Error)> CreateStaffUserAsync(CreateStaffUserRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/users/staff", request, JsonOptions);
        return await ReadAsync<UserDto>(response);
    }

    public async Task<(bool Success, UserDto? User, string? Error)> UpdateStaffUserAsync(int id, UpdateStaffUserRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/users/staff/{id}", request, JsonOptions);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return (false, null, "Staff user not found");
        return await ReadAsync<UserDto>(response);
    }

    public async Task<(bool Success, string? Error)> DeleteStaffUserAsync(int id)
    {
        var response = await httpClient.DeleteAsync($"api/users/staff/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return (false, "Staff user not found");
        if (!response.IsSuccessStatusCode)
            return (false, await ApiResponseReader.ReadErrorMessageAsync(response));
        return (true, null);
    }

    public async Task<(bool Success, PaginatedResult<MotorcycleDto>? Result, string? Error)> GetMotorcyclesAsync(
        string? search,
        MotorcycleStatus? status,
        decimal? minPrice,
        decimal? maxPrice,
        int pageNumber,
        int pageSize = 20)
    {
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            filters.Add(ODataQuery.ContainsAny(search, "LicensePlate", "Brand", "Model", "RegistrationNo"));
        if (status.HasValue)
            filters.Add($"StatusCode eq {(int)status.Value}");
        if (minPrice.HasValue)
            filters.Add($"DailyRate ge {ODataQuery.DecimalLiteral(minPrice.Value)}");
        if (maxPrice.HasValue)
            filters.Add($"DailyRate le {ODataQuery.DecimalLiteral(maxPrice.Value)}");

        var query = ODataQuery.BuildCollectionUrl("Motorcycles", filters, "CreatedAt desc", pageNumber, pageSize);
        var response = await httpClient.GetAsync(query);
        return await ReadODataPageAsync<MotorcycleDto>(response, pageNumber, pageSize);
    }

    public Task<(bool Success, MotorcycleDetailDto? Motorcycle, string? Error)> GetMotorcycleAsync(int id)
        => ReadMotorcycleDetailAsync(id);

    public async Task<(bool Success, MotorcycleDto? Motorcycle, string? Error)> CreateMotorcycleAsync(
        CreateMotorcycleRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/motorcycles", request, JsonOptions);
        return await ReadAsync<MotorcycleDto>(response);
    }

    public async Task<(bool Success, MotorcycleDto? Motorcycle, string? Error)> UpdateMotorcycleAsync(
        int id,
        UpdateMotorcycleRequest request)
    {
        var response = await httpClient.PutAsJsonAsync($"api/motorcycles/{id}", request, JsonOptions);
        return await ReadAsync<MotorcycleDto>(response);
    }

    public async Task<(bool Success, string? Error)> DeleteMotorcycleAsync(int id)
    {
        var response = await httpClient.DeleteAsync($"api/motorcycles/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return (false, "Motorcycle not found");
        if (!response.IsSuccessStatusCode)
            return (false, await ApiResponseReader.ReadErrorMessageAsync(response));
        return (true, null);
    }

    public async Task<(bool Success, List<MotorcycleTypeDto>? Types, string? Error)> GetMotorcycleTypesAsync()
    {
        var response = await httpClient.GetAsync("api/motorcycletypes");
        return await ReadAsync<List<MotorcycleTypeDto>>(response);
    }

    public async Task<(bool Success, List<MyRentalContractDto>? Contracts, string? Error)> GetMyRentalContractsAsync()
    {
        var response = await httpClient.GetAsync("api/my-rental-contracts");
        return await ReadAsync<List<MyRentalContractDto>>(response);
    }

    public async Task<(bool Success, PaginatedResult<MaintenanceRecordDto>? Result, string? Error)> GetMaintenanceRecordsAsync(
        int? motorcycleId,
        MaintenanceStatus? status,
        int pageNumber,
        int pageSize = 10)
    {
        var filters = new List<string>();
        if (motorcycleId.HasValue)
            filters.Add($"MotorcycleId eq {motorcycleId.Value}");
        if (status.HasValue)
            filters.Add($"StatusCode eq {(int)status.Value}");

        var query = ODataQuery.BuildCollectionUrl("MaintenanceRecords", filters, "CreatedAt desc", pageNumber, pageSize);
        var response = await httpClient.GetAsync(query);
        return await ReadODataPageAsync<MaintenanceRecordDto>(response, pageNumber, pageSize);
    }

    public async Task<(bool Success, MaintenanceRecordDto? Record, string? Error)> CreateMaintenanceRecordAsync(
        MaintenanceRecordCreateDto request)
    {
        var response = await httpClient.PostAsJsonAsync("api/maintenance-records", request, JsonOptions);
        return await ReadAsync<MaintenanceRecordDto>(response);
    }

    public async Task<(bool Success, List<PlaceSuggestionDto>? Places, string? Error)> SearchPlacesAsync(string query)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/routes/search?query={Uri.EscapeDataString(query)}");
            return await ReadAsync<List<PlaceSuggestionDto>>(response);
        }
        catch (HttpRequestException)
        {
            return (false, null, "Cannot connect to the route API. Ensure the API is running on port 5210.");
        }
        catch (TaskCanceledException)
        {
            return (false, null, "The route API request timed out.");
        }
    }

    public async Task<(bool Success, RouteAssistantResponseDto? Response, string? Error)> AskRouteAssistantAsync(
        RouteAssistantRequestDto request)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/routes/assistant", request, JsonOptions);
            return await ReadAsync<RouteAssistantResponseDto>(response);
        }
        catch (HttpRequestException)
        {
            return (false, null, "Cannot connect to the route API. Ensure the API is running on port 5210.");
        }
        catch (TaskCanceledException)
        {
            return (false, null, "The route assistant request timed out.");
        }
    }

    public async Task<(bool Success, RouteResultDto? Route, string? Error)> ComputeRouteAsync(
        ComputeRouteRequestDto request)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync("api/routes/compute", request, JsonOptions);
            return await ReadAsync<RouteResultDto>(response);
        }
        catch (HttpRequestException)
        {
            return (false, null, "Cannot connect to the route API. Ensure the API is running on port 5210.");
        }
        catch (TaskCanceledException)
        {
            return (false, null, "The route API request timed out.");
        }
    }

    private async Task<(bool Success, MotorcycleDetailDto? Motorcycle, string? Error)> ReadMotorcycleDetailAsync(int id)
    {
        var response = await httpClient.GetAsync($"api/motorcycles/{id}");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return (false, null, "Motorcycle not found");
        return await ReadAsync<MotorcycleDetailDto>(response);
    }

    private static async Task<(bool Success, PaginatedResult<T>? Result, string? Error)> ReadODataPageAsync<T>(
        HttpResponseMessage response,
        int pageNumber,
        int pageSize)
    {
        var (success, data, error) = await ReadAsync<ODataResponse<T>>(response);
        return !success || data == null
            ? (false, null, error)
            : (true, data.ToPaginatedResult(pageNumber, pageSize), null);
    }

    private static async Task<(bool Success, T? Data, string? Error)> ReadAsync<T>(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return (false, default, "Phiên API đã hết hạn. Vui lòng đăng nhập lại.");

        if (!response.IsSuccessStatusCode)
            return (false, default, await ApiResponseReader.ReadErrorMessageAsync(response));

        var data = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return (true, data, null);
    }
}
