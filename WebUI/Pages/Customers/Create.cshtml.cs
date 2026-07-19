using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Services.DTOs;
using WebUI.Services.Internal;

namespace WebUI.Pages.Customers;

public class CreateModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;

    public CreateModel(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [BindProperty]
    public CustomerCreateForm Form { get; set; } = new();

    public string DefaultPassword => CustomerCreateDto.DefaultPassword;

    public IActionResult OnGet()
    {
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var client = _httpClientFactory.CreateClient("GobikeApi");
        var response = await client.PostAsJsonAsync("/api/customer", Form);

        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(string.Empty, await ApiResponseReader.ReadErrorMessageAsync(response));
            return Page();
        }

        TempData["CustomerCreated"] =
            $"Customer and login account created. Username: {Form.Username}. Default password: {CustomerCreateDto.DefaultPassword}.";
        return RedirectToPage("./Index");
    }
}

public class CustomerCreateForm
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^[A-Za-z0-9._-]+$", ErrorMessage = "Username may only contain letters, numbers, dots, underscores, and hyphens.")]
    public string Username { get; set; } = "";

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, MinimumLength = 2)]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "CCCD is required.")]
    [RegularExpression(@"^[0-9]{12}$", ErrorMessage = "CCCD must be exactly 12 digits.")]
    public string Cccd { get; set; } = "";

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^0[0-9]{9,10}$", ErrorMessage = "Phone must be 10-11 digits starting with 0.")]
    public string PhoneNumber { get; set; } = "";

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = "";

    public string? Address { get; set; }

    [Required(ErrorMessage = "Date of birth is required.")]
    public DateTime DateOfBirth { get; set; }

    [Required(ErrorMessage = "Driver license is required.")]
    [StringLength(20, ErrorMessage = "Driver license must be 20 characters or fewer.")]
    public string DriverLicenseNo { get; set; } = "";
}
