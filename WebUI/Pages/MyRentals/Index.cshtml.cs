using BusinessObjects.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Services.DTOs;
using WebUI.Helpers;
using WebUI.Services;

namespace WebUI.Pages.MyRentals;

[Authorize(Roles = "Customer")]
public sealed class IndexModel : PageModel
{
    private readonly IGoBikeApiClient apiClient;

    public IndexModel(IGoBikeApiClient apiClient)
    {
        this.apiClient = apiClient;
    }

    public List<MyRentalContractDto> CurrentRentals { get; private set; } = [];
    public List<MyRentalContractDto> RentalHistory { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var (success, contracts, error) = await apiClient.GetMyRentalContractsAsync();
        var redirect = await ApiPageHelper.HandleApiAuthFailureAsync(this, error, apiClient);
        if (redirect != null)
            return redirect;

        if (!success || contracts == null)
        {
            ErrorMessage = error ?? "Unable to load your rental contracts.";
            return Page();
        }

        CurrentRentals = contracts
            .Where(contract => contract.Status is (int)RentalStatus.Reserved or (int)RentalStatus.Active)
            .ToList();
        RentalHistory = contracts
            .Where(contract => contract.Status is not ((int)RentalStatus.Reserved) and not ((int)RentalStatus.Active))
            .ToList();

        return Page();
    }

    public static string GetStatusName(int status) => Enum.IsDefined(typeof(RentalStatus), status)
        ? ((RentalStatus)status).ToString()
        : "Unknown";

    public static string GetStatusBadgeClass(int status) => (RentalStatus)status switch
    {
        RentalStatus.Reserved => "text-bg-warning",
        RentalStatus.Active => "text-bg-primary",
        RentalStatus.Completed => "text-bg-success",
        RentalStatus.Cancelled => "text-bg-secondary",
        RentalStatus.NoShow => "text-bg-danger",
        _ => "text-bg-light"
    };
}
