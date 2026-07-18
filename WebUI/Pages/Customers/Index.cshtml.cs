using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebUI.Services;

namespace WebUI.Pages.Customers;

public class IndexModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public IndexModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public List<CustomerItem> Customers { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public string? SearchQuery { get; set; }

    public static string Mask(string value) =>
        value.Length >= 8 ? value[..4] + "****" + value[^4..] : value;

    public async Task OnGetAsync(string? search, int page = 1)
    {
        SearchQuery = search;
        CurrentPage = page < 1 ? 1 : page;

        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(search))
            filters.Add(ODataQuery.ContainsAny(search, "FullName", "CCCD", "PhoneNumber"));

        var client = _httpClientFactory.CreateClient("GobikeApi");
        var url = ODataQuery.BuildCollectionUrl("Customers", filters, "CreatedAt desc", CurrentPage, 10);

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ODataResponse<CustomerItem>>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ODataResponse<CustomerItem>();
        Customers = result.Value;
        TotalPages = Math.Max(1, (int)Math.Ceiling((result.Count ?? 0) / 10d));
    }

    public static int CalculateAge(DateTime dateOfBirth)
    {
        var today = DateTime.Today;
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth.Date > today.AddYears(-age)) age--;
        return age;
    }
}

public class CustomerItem
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Cccd { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime DateOfBirth { get; set; }
    public DateTime CreatedAt { get; set; }
}
