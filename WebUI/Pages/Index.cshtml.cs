using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessObjects;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Services.DTOs;

namespace WebUI.Pages;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public int NewCustomersThisWeek { get; set; }
    public int RentalsThisWeek { get; set; }
    public int ActiveRentals { get; set; }
    public string TopVehicleTypeName { get; set; } = "No data";
    public int TopVehicleTypeRentalCount { get; set; }
    public List<WeeklyCustomerPoint> WeeklyCustomers { get; set; } = [];
    public List<TopVehicleTypePoint> TopVehicleTypes { get; set; } = [];

    public async Task OnGetAsync()
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Staff"))
            return;

        var client = _httpClientFactory.CreateClient("GobikeApi");
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        var customers = await ReadListAsync<CustomerDashboardItem>(client, "/api/customer?page=1&pageSize=1000", jsonOptions);
        var rentals = await ReadListAsync<RentalDashboardItem>(client, "/api/rental-contracts?page=1&pageSize=1000", jsonOptions);
        var motorcycles = await ReadMotorcyclesAsync(client, jsonOptions);

        BuildWeeklyCustomers(customers);
        BuildRentalHighlights(rentals, motorcycles);
    }

    private void BuildWeeklyCustomers(List<CustomerDashboardItem> customers)
    {
        var currentWeekStart = GetWeekStart(SystemClock.Today);
        var weeks = Enumerable.Range(0, 6)
            .Select(offset => currentWeekStart.AddDays(-7 * (5 - offset)))
            .ToList();

        WeeklyCustomers = weeks
            .Select(start =>
            {
                var end = start.AddDays(7);
                return new WeeklyCustomerPoint
                {
                    Label = start == currentWeekStart ? "This week" : $"{start:dd/MM}",
                    StartDate = start,
                    EndDate = end.AddDays(-1),
                    Count = customers.Count(c => c.CreatedAt.Date >= start && c.CreatedAt.Date < end)
                };
            })
            .ToList();

        NewCustomersThisWeek = WeeklyCustomers.LastOrDefault()?.Count ?? 0;
    }

    private void BuildRentalHighlights(List<RentalDashboardItem> rentals, List<MotorcycleDto> motorcycles)
    {
        var currentWeekStart = GetWeekStart(SystemClock.Today);
        var nextWeekStart = currentWeekStart.AddDays(7);
        var motorcycleTypesByPlate = motorcycles
            .Where(m => !string.IsNullOrWhiteSpace(m.LicensePlate))
            .GroupBy(m => m.LicensePlate, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().VehicleTypeName, StringComparer.OrdinalIgnoreCase);

        RentalsThisWeek = rentals.Count(r => r.RentalDate.Date >= currentWeekStart && r.RentalDate.Date < nextWeekStart);
        ActiveRentals = rentals.Count(r => r.Status == 2);

        TopVehicleTypes = rentals
            .Where(r => !string.IsNullOrWhiteSpace(r.MotorcycleLicensePlate))
            .Select(r =>
            {
                var hasType = motorcycleTypesByPlate.TryGetValue(r.MotorcycleLicensePlate!, out var typeName);
                return string.IsNullOrWhiteSpace(typeName) ? "Unknown" : typeName;
            })
            .GroupBy(typeName => typeName)
            .Select(g => new TopVehicleTypePoint
            {
                VehicleTypeName = g.Key,
                RentalCount = g.Count()
            })
            .OrderByDescending(x => x.RentalCount)
            .ThenBy(x => x.VehicleTypeName)
            .Take(5)
            .ToList();

        var top = TopVehicleTypes.FirstOrDefault();
        if (top != null)
        {
            TopVehicleTypeName = top.VehicleTypeName;
            TopVehicleTypeRentalCount = top.RentalCount;
        }
    }

    private static async Task<List<T>> ReadListAsync<T>(HttpClient client, string url, JsonSerializerOptions jsonOptions)
    {
        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<T>>(json, jsonOptions) ?? [];
    }

    private static async Task<List<MotorcycleDto>> ReadMotorcyclesAsync(HttpClient client, JsonSerializerOptions jsonOptions)
    {
        var response = await client.GetAsync("/api/motorcycles?page=1&pageSize=1000");
        if (!response.IsSuccessStatusCode)
            return [];

        var json = await response.Content.ReadAsStringAsync();
        var page = JsonSerializer.Deserialize<PaginatedResult<MotorcycleDto>>(json, jsonOptions);
        return page?.Items ?? [];
    }

    private static DateTime GetWeekStart(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-diff);
    }
}

public class WeeklyCustomerPoint
{
    public string Label { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Count { get; set; }
}

public class TopVehicleTypePoint
{
    public string VehicleTypeName { get; set; } = "";
    public int RentalCount { get; set; }
}

public class CustomerDashboardItem
{
    public DateTime CreatedAt { get; set; }
}

public class RentalDashboardItem
{
    public string? MotorcycleLicensePlate { get; set; }
    public DateTime RentalDate { get; set; }
    public int Status { get; set; }
}
