using System.ComponentModel.DataAnnotations;
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
public class CreateModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _environment;

    public CreateModel(IHttpClientFactory httpClientFactory, IWebHostEnvironment environment)
    {
        _httpClientFactory = httpClientFactory;
        _environment = environment;
    }

    [BindProperty]
    public RentalCreateForm Form { get; set; } = new();

    public List<AvailableMotorcycle> AvailableMotorcycles { get; set; } = new();
    public List<CustomerOption> Customers { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
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
            AvailableMotorcycles = JsonSerializer.Deserialize<List<AvailableMotorcycle>>(json, jsonOptions) ?? new();
        }

        var custRes = await client.GetAsync("/api/customer");
        if (custRes.IsSuccessStatusCode)
        {
            var json = await custRes.Content.ReadAsStringAsync();
            Customers = JsonSerializer.Deserialize<List<CustomerOption>>(json, jsonOptions) ?? new();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile? depositProofImage)
    {
        if (!ModelState.IsValid)
        {
            return await OnGetAsync();
        }

        if (Form.CustomerId <= 0)
        {
            ModelState.AddModelError("Form.CustomerId", "Customer is required.");
            return await OnGetAsync();
        }

        if (Form.MotorcycleId <= 0)
        {
            ModelState.AddModelError("Form.MotorcycleId", "Motorcycle is required.");
            return await OnGetAsync();
        }

        if (Form.RentalDate.Date < SystemClock.Today)
        {
            ModelState.AddModelError("Form.RentalDate", "Rental date cannot be in the past.");
            return await OnGetAsync();
        }

        if (Form.ExpectedReturnDate < Form.RentalDate)
        {
            ModelState.AddModelError("Form.ExpectedReturnDate", "Expected return date must be on or after rental date.");
            return await OnGetAsync();
        }

        if (!Form.DepositConfirmed)
        {
            ModelState.AddModelError("Form.DepositConfirmed", "Deposit must be collected before creating a reservation.");
            return await OnGetAsync();
        }

        var (depositProofImageUrl, proofImageError) = await DepositProofImageHelper.SaveAsync(_environment, depositProofImage);
        if (proofImageError != null)
        {
            ModelState.AddModelError("depositProofImage", proofImageError);
            return await OnGetAsync();
        }

        var payload = new
        {
            customerId = Form.CustomerId,
            motorcycleId = Form.MotorcycleId,
            startDate = Form.RentalDate.ToString("yyyy-MM-dd"),
            endDate = Form.ExpectedReturnDate.ToString("yyyy-MM-dd"),
            depositConfirmed = Form.DepositConfirmed,
            depositPaymentMethod = Form.DepositPaymentMethod,
            depositPaymentNote = Form.DepositPaymentNote,
            depositProofImageUrl,
            notes = Form.Notes
        };

        var client = _httpClientFactory.CreateClient("GobikeApi");
        var response = await client.PostAsJsonAsync("/api/rental-contracts/reserve", payload);

        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, await ReadErrorMessageAsync(response));
            return await OnGetAsync();
        }

        var resultJson = await response.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<RentalCreatedResult>(resultJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        TempData?["SuccessMessage"] = "Rental reservation created successfully.";
        return RedirectToPage("./Details", new { id = created?.Id ?? 0 });
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        var fallback = "Failed to create rental contract. Please try again.";
        var json = await response.Content.ReadAsStringAsync();
        if (string.IsNullOrWhiteSpace(json))
            return fallback;

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
