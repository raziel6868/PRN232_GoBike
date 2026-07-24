using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Services.DTOs;
using WebUI.Services.Internal;

namespace WebUI.Pages.Customers;

public class EditModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public EditModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty]
    public CustomerUpdateDto Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var client = _httpClientFactory.CreateClient("GobikeApi");
        var response = await client.GetAsync($"/api/customer/{id}");

        if (!response.IsSuccessStatusCode)
        {
            return NotFound();
        }

        var json = await response.Content.ReadAsStringAsync();
        var customer = JsonSerializer.Deserialize<CustomerUpdateDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (customer == null) return NotFound();

        Form = customer;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (id <= 0 || Form.Id != id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var client = _httpClientFactory.CreateClient("GobikeApi");
        var response = await client.PutAsJsonAsync($"/api/customer/{id}", Form);

        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, await ApiResponseReader.ReadErrorMessageAsync(response));
            return Page();
        }

        return RedirectToPage("./Index");
    }
}
