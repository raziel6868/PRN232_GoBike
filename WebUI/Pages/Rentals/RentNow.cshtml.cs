using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebUI.Helpers;

namespace WebUI.Pages.Rentals;

[Authorize(Roles = "Admin,Staff")]
public class RentNowModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;

    public RentNowModel(IHttpClientFactory httpClientFactory, IWebHostEnvironment environment)
    {
        _httpClientFactory = httpClientFactory;
        _environment = environment;
    }

    [BindProperty]
    public RentalRentNowForm Form { get; set; } = new();

    public List<AvailableMotorcycle> AvailableMotorcycles { get; set; } = [];
    public List<CustomerOption> Customers { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        Form.RentalDate = SystemClock.Today;
        Form.ExpectedReturnDate = SystemClock.Today.AddDays(1);
        Form.FuelLevel = "Full";
        Form.VehicleCondition = "Good";
        Form.DepositPaymentMethod = 1;

        await LoadOptionsAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? depositProofImage)
    {
        Form.RentalDate = SystemClock.Today;

        if (Form.CustomerId <= 0)
            ModelState.AddModelError("Form.CustomerId", "Customer is required.");

        if (Form.MotorcycleId <= 0)
            ModelState.AddModelError("Form.MotorcycleId", "Motorcycle is required.");

        if (Form.ExpectedReturnDate.Date < SystemClock.Today)
            ModelState.AddModelError("Form.ExpectedReturnDate", "Expected return date must be today or later.");

        if (!Form.DepositConfirmed)
            ModelState.AddModelError("Form.DepositConfirmed", "Deposit must be collected before creating a rent-now contract.");

        if (Form.StartMileage < 0)
            ModelState.AddModelError("Form.StartMileage", "Start mileage must be 0 or greater.");

        if (string.IsNullOrWhiteSpace(Form.FuelLevel))
            ModelState.AddModelError("Form.FuelLevel", "Fuel level is required.");

        if (string.IsNullOrWhiteSpace(Form.VehicleCondition))
            ModelState.AddModelError("Form.VehicleCondition", "Vehicle condition is required.");

        if (Form.HasDamage && string.IsNullOrWhiteSpace(Form.DamageDescription))
            ModelState.AddModelError("Form.DamageDescription", "Damage description is required when damage is reported.");

        if (!ModelState.IsValid)
        {
            await LoadOptionsAsync();
            return Page();
        }

        var (depositProofImageUrl, proofImageError) = await DepositProofImageHelper.SaveAsync(_environment, depositProofImage);
        if (proofImageError != null)
        {
            ModelState.AddModelError("depositProofImage", proofImageError);
            await LoadOptionsAsync();
            return Page();
        }

        var payload = new
        {
            customerId = Form.CustomerId,
            motorcycleId = Form.MotorcycleId,
            startDate = SystemClock.Today.ToString("yyyy-MM-dd"),
            endDate = Form.ExpectedReturnDate.ToString("yyyy-MM-dd"),
            depositConfirmed = Form.DepositConfirmed,
            depositPaymentMethod = Form.DepositPaymentMethod,
            depositPaymentNote = Form.DepositPaymentNote,
            depositProofImageUrl,
            notes = Form.Notes,
            beforeInspection = new
            {
                mileage = Form.StartMileage,
                fuelLevel = Form.FuelLevel,
                vehicleCondition = Form.VehicleCondition,
                hasDamage = Form.HasDamage,
                damageDescription = Form.DamageDescription,
                accessoriesNote = Form.AccessoriesNote,
                note = Form.InspectionNote
            }
        };

        var client = _httpClientFactory.CreateClient("GobikeApi");
        var response = await client.PostAsJsonAsync("/api/rental-contracts/rent-now", payload);

        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, await ReadErrorMessageAsync(response));
            await LoadOptionsAsync();
            return Page();
        }

        var resultJson = await response.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<RentalCreatedResult>(resultJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        TempData?["SuccessMessage"] = "Rent-now contract created successfully.";
        return RedirectToPage("./Details", new { id = created?.Id ?? 0 });
    }

    private async Task LoadOptionsAsync()
    {
        var client = _httpClientFactory.CreateClient("GobikeApi");
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var motoRes = await client.GetAsync("/api/motorcycles/available");
        if (motoRes.IsSuccessStatusCode)
        {
            var json = await motoRes.Content.ReadAsStringAsync();
            AvailableMotorcycles = JsonSerializer.Deserialize<List<AvailableMotorcycle>>(json, jsonOptions) ?? [];
        }

        var custRes = await client.GetAsync("/api/customer");
        if (custRes.IsSuccessStatusCode)
        {
            var json = await custRes.Content.ReadAsStringAsync();
            Customers = JsonSerializer.Deserialize<List<CustomerOption>>(json, jsonOptions) ?? [];
        }
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        var fallback = "Failed to create rent-now contract. Please try again.";
        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json))
            return fallback;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("message", out var message))
                return message.GetString() ?? fallback;

            if (document.RootElement.TryGetProperty("errors", out var errors))
                return errors.ToString();

            if (document.RootElement.TryGetProperty("title", out var title))
                return title.GetString() ?? fallback;
        }
        catch (JsonException)
        {
            return json;
        }

        return fallback;
    }
}
