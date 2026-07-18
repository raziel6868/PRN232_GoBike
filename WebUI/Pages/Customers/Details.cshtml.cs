using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebUI.Services;

namespace WebUI.Pages.Customers;

public class DetailsModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DetailsModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public CustomerDetail Customer { get; set; } = new();
    public List<RentalHistoryItem> RentalHistory { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var client = _httpClientFactory.CreateClient("GobikeApi");

        var custRes = await client.GetAsync($"/api/customer/{id}");
        if (!custRes.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var custJson = await custRes.Content.ReadAsStringAsync();
        Customer = JsonSerializer.Deserialize<CustomerDetail>(custJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new CustomerDetail();

        var rentalUrl = ODataQuery.BuildCollectionUrl(
            "RentalContracts",
            [$"CustomerId eq {id}"],
            "RentalDate desc",
            1,
            100);
        var rentalRes = await client.GetAsync(rentalUrl);
        if (rentalRes.IsSuccessStatusCode)
        {
            var rentalJson = await rentalRes.Content.ReadAsStringAsync();
            var rentals = JsonSerializer.Deserialize<ODataResponse<RentalHistoryItem>>(rentalJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            RentalHistory = rentals?.Value ?? [];
        }

        return Page();
    }

    public static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age)) age--;
        return age;
    }
}

public class CustomerDetail
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Cccd { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Address { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string DriverLicenseNo { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class RentalHistoryItem
{
    public int Id { get; set; }
    public DateTime RentalDate { get; set; }
    public string? MotorcycleLicensePlate { get; set; }
    public int Status { get; set; }
    public decimal TotalAmount { get; set; }
}
