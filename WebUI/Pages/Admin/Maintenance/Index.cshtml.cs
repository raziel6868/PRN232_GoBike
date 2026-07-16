using BusinessObjects;
using BusinessObjects.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Services.DTOs;
using WebUI.Helpers;
using WebUI.Services;

namespace WebUI.Pages.Admin.Maintenance;

[Authorize(Roles = "Admin")]
public class IndexModel : PageModel
{
    private readonly IGoBikeApiClient apiClient;

    public IndexModel(IGoBikeApiClient apiClient)
    {
        this.apiClient = apiClient;
    }

    public PaginatedResult<MaintenanceRecordDto> Result { get; set; } = new();
    public List<MotorcycleDto> EligibleMotorcycles { get; set; } = [];
    public string? ErrorMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? MotorcycleId { get; set; }

    [BindProperty(SupportsGet = true)]
    public MaintenanceStatus? Status { get; set; }

    [BindProperty(SupportsGet = true, Name = "page")]
    public int PageNumber { get; set; } = 1;

    public async Task<IActionResult> OnGetAsync()
    {
        var (success, result, error) = await apiClient.GetMaintenanceRecordsAsync(
            MotorcycleId, Status, PageNumber);
        var redirect = await ApiPageHelper.HandleApiAuthFailureAsync(this, error, apiClient);
        if (redirect != null)
            return redirect;

        if (success && result != null)
            Result = result;
        else
            ErrorMessage = error ?? "Failed to load maintenance records.";

        await LoadEligibleMotorcyclesAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync([FromForm] MaintenanceRecordCreateDto request)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please provide a motorcycle, reason, valid repair cost, and start date.";
            return RedirectToPage();
        }

        var (success, _, error) = await apiClient.CreateMaintenanceRecordAsync(request);
        var redirect = await ApiPageHelper.HandleApiAuthFailureAsync(this, error, apiClient);
        if (redirect != null)
            return redirect;

        TempData[success ? "Success" : "Error"] = success
            ? "Maintenance record created. The motorcycle is now marked for maintenance."
            : error ?? "Failed to create maintenance record.";

        return RedirectToPage();
    }

    public static string GetStatusBadgeClass(MaintenanceStatus status) => status switch
    {
        MaintenanceStatus.Pending => "badge-warning",
        MaintenanceStatus.InProgress => "badge-info",
        MaintenanceStatus.Completed => "badge-completed",
        MaintenanceStatus.Cancelled => "badge-cancelled",
        _ => "bg-light text-dark"
    };

    private async Task LoadEligibleMotorcyclesAsync()
    {
        var availableTask = apiClient.GetMotorcyclesAsync(
            null, MotorcycleStatus.Available, null, null, 1, 100);
        var maintenanceTask = apiClient.GetMotorcyclesAsync(
            null, MotorcycleStatus.Maintenance, null, null, 1, 100);

        await Task.WhenAll(availableTask, maintenanceTask);
        var available = await availableTask;
        var maintenance = await maintenanceTask;

        if (!available.Success || !maintenance.Success)
        {
            ErrorMessage ??= available.Error ?? maintenance.Error ?? "Failed to load motorcycles for maintenance.";
            return;
        }

        EligibleMotorcycles = (available.Result?.Items ?? [])
            .Concat(maintenance.Result?.Items ?? [])
            .OrderBy(motorcycle => motorcycle.LicensePlate)
            .ToList();
    }
}
