using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessObjects.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Services.DTOs;
using WebUI.Services;
using WebUI.Services.Internal;

namespace WebUI.Pages.Rentals;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public List<RentalListItem> Rentals { get; set; } = new();
    public List<CustomerOption> Customers { get; set; } = new();
    public List<MotorcycleOption> Motorcycles { get; set; } = new();
    public string? ErrorMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FilterCustomerId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FilterMotorcycleId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterStatus { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FilterFromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FilterToDate { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var client = _httpClientFactory.CreateClient("GobikeApi");
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var rentalFilters = new List<string>();
        if (FilterCustomerId.HasValue)
            rentalFilters.Add($"CustomerId eq {FilterCustomerId.Value}");
        if (FilterMotorcycleId.HasValue)
            rentalFilters.Add($"MotorcycleId eq {FilterMotorcycleId.Value}");
        if (Enum.TryParse<RentalStatus>(FilterStatus, true, out var status))
            rentalFilters.Add($"Status eq {(int)status}");
        if (FilterFromDate.HasValue)
            rentalFilters.Add($"RentalDate ge {ODataQuery.DateTimeOffsetLiteral(FilterFromDate.Value.Date)}");
        if (FilterToDate.HasValue)
            rentalFilters.Add($"RentalDate lt {ODataQuery.DateTimeOffsetLiteral(FilterToDate.Value.Date.AddDays(1))}");

        var customerTask = client.GetAsync(ODataQuery.BuildCollectionUrl("Customers", [], "FullName asc", 1, 100));
        var motorcycleTask = client.GetAsync(ODataQuery.BuildCollectionUrl("Motorcycles", [], "LicensePlate asc", 1, 100));
        var rentalTask = client.GetAsync(ODataQuery.BuildCollectionUrl("RentalContracts", rentalFilters, "CreatedAt desc", 1, 100));
        await Task.WhenAll(customerTask, motorcycleTask, rentalTask);

        var custRes = await customerTask;
        if (custRes.IsSuccessStatusCode)
        {
            var custJson = await custRes.Content.ReadAsStringAsync();
            Customers = (JsonSerializer.Deserialize<ODataResponse<CustomerOption>>(custJson, jsonOptions)?.Value) ?? [];
        }

        var motoRes = await motorcycleTask;
        if (motoRes.IsSuccessStatusCode)
        {
            var motoJson = await motoRes.Content.ReadAsStringAsync();
            Motorcycles = (JsonSerializer.Deserialize<ODataResponse<MotorcycleOption>>(motoJson, jsonOptions)?.Value) ?? [];
        }

        var rentalRes = await rentalTask;
        if (rentalRes.IsSuccessStatusCode)
        {
            var rentalJson = await rentalRes.Content.ReadAsStringAsync();
            Rentals = (JsonSerializer.Deserialize<ODataResponse<RentalListItem>>(rentalJson, jsonOptions)?.Value) ?? [];
        }
        else
        {
            ErrorMessage = await ApiResponseReader.ReadErrorMessageAsync(rentalRes);
        }

        return Page();
    }

    public static string GetStatusBadgeClass(int status) => status switch
    {
        2 => "badge-success",
        1 => "badge-warning",
        3 => "badge-completed",
        4 => "badge-cancelled",
        5 => "badge-noshow",
        _ => "badge-info"
    };

    public static string GetStatusName(int status) => status switch
    {
        1 => "Reserved",
        2 => "Active",
        3 => "Completed",
        4 => "Cancelled",
        5 => "NoShow",
        _ => "Unknown"
    };
}
