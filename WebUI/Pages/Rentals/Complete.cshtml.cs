using System.Net.Http.Json;
using System.Text.Json;
using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebUI.Pages.Rentals;

[Authorize(Roles = "Admin,Staff")]
public class CompleteModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CompleteModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public RentalDetail Rental { get; set; } = new();
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public DateTime ActualReturnDate { get; set; }

    [BindProperty]
    public int EndMileage { get; set; }

    [BindProperty]
    public string FuelLevel { get; set; } = "Full";

    [BindProperty]
    public string VehicleCondition { get; set; } = "Good";

    [BindProperty]
    public bool HasDamage { get; set; }

    [BindProperty]
    public string? DamageDescription { get; set; }

    [BindProperty]
    public decimal DamageFee { get; set; }

    [BindProperty]
    public decimal OtherFee { get; set; }

    [BindProperty]
    public string? OtherFeeDescription { get; set; }

    [BindProperty]
    public decimal DiscountAmount { get; set; }

    [BindProperty]
    public string? DiscountReason { get; set; }

    [BindProperty]
    public int MotorcycleStatusAfterReturn { get; set; } = 1;

    [BindProperty]
    public int SettlementPaymentMethod { get; set; } = 1;

    [BindProperty]
    public string? SettlementPaymentNote { get; set; }

    [BindProperty]
    public string? AccessoriesNote { get; set; }

    [BindProperty]
    public string? InspectionNote { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        if (!await LoadRentalAsync(id)) return NotFound();

        ActualReturnDate = SystemClock.Today;
        EndMileage = Rental.StartMileage ?? Rental.MotorcycleMileage ?? Rental.EndMileage ?? 0;
        FuelLevel = "Full";
        VehicleCondition = "Good";
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (!await LoadRentalAsync(id)) return NotFound();

        if (EndMileage < 0)
        {
            ModelState.AddModelError(nameof(EndMileage), "End mileage must be 0 or greater.");
            return Page();
        }

        if (Rental.StartMileage.HasValue && EndMileage < Rental.StartMileage.Value)
        {
            ModelState.AddModelError(
                nameof(EndMileage),
                $"End mileage must be at least the start mileage ({Rental.StartMileage.Value} km).");
            return Page();
        }

        if (ActualReturnDate == default)
        {
            ModelState.AddModelError(nameof(ActualReturnDate), "Actual return date is required.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(FuelLevel))
        {
            ModelState.AddModelError(nameof(FuelLevel), "Fuel level is required.");
            return Page();
        }

        if (string.IsNullOrWhiteSpace(VehicleCondition))
        {
            ModelState.AddModelError(nameof(VehicleCondition), "Vehicle condition is required.");
            return Page();
        }

        if ((HasDamage || DamageFee > 0) && string.IsNullOrWhiteSpace(DamageDescription))
        {
            ModelState.AddModelError(nameof(DamageDescription), "Damage description is required when damage is reported.");
            return Page();
        }

        if (OtherFee > 0 && string.IsNullOrWhiteSpace(OtherFeeDescription))
        {
            ModelState.AddModelError(nameof(OtherFeeDescription), "Other fee description is required when other fee is greater than zero.");
            return Page();
        }

        if (DiscountAmount > 0 && string.IsNullOrWhiteSpace(DiscountReason))
        {
            ModelState.AddModelError(nameof(DiscountReason), "Discount reason is required when discount is greater than zero.");
            return Page();
        }

        if (DamageFee < 0 || OtherFee < 0 || DiscountAmount < 0)
        {
            ModelState.AddModelError(string.Empty, "Fees and discount must be 0 or greater.");
            return Page();
        }

        var client = _httpClientFactory.CreateClient("GobikeApi");
        var payload = new
        {
            actualReturnDate = ActualReturnDate.ToString("yyyy-MM-dd"),
            afterInspection = new
            {
                mileage = EndMileage,
                fuelLevel = FuelLevel,
                vehicleCondition = VehicleCondition,
                hasDamage = HasDamage,
                damageDescription = DamageDescription,
                accessoriesNote = AccessoriesNote,
                note = InspectionNote
            },
            damageFee = DamageFee,
            damageDescription = DamageDescription,
            otherFee = OtherFee,
            otherFeeDescription = OtherFeeDescription,
            discountAmount = DiscountAmount,
            discountReason = DiscountReason,
            motorcycleStatusAfterReturn = MotorcycleStatusAfterReturn,
            settlementPaymentMethod = SettlementPaymentMethod,
            settlementPaymentNote = SettlementPaymentNote
        };

        var response = await client.PostAsJsonAsync($"/api/rental-contracts/{id}/complete", payload);

        if (!response.IsSuccessStatusCode)
        {
            ErrorMessage = await ReadErrorMessageAsync(response, "Failed to complete rental contract.");
            return Page();
        }

        TempData?["SuccessMessage"] = "Rental contract completed successfully.";
        return RedirectToPage("./Details", new { id });
    }

    private async Task<bool> LoadRentalAsync(int id)
    {
        var client = _httpClientFactory.CreateClient("GobikeApi");
        var response = await client.GetAsync($"/api/rental-contracts/{id}");

        if (!response.IsSuccessStatusCode) return false;

        var json = await response.Content.ReadAsStringAsync();
        Rental = JsonSerializer.Deserialize<RentalDetail>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new RentalDetail();

        return true;
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, string fallback)
    {
        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json)) return fallback;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("message", out var message))
                return message.GetString() ?? fallback;

            if (document.RootElement.TryGetProperty("title", out var title))
                return title.GetString() ?? fallback;

            if (document.RootElement.TryGetProperty("errors", out var errors))
                return errors.ToString();
        }
        catch (JsonException)
        {
            return json;
        }

        return fallback;
    }
}
