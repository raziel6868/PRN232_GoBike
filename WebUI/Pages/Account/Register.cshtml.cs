using BusinessObjects.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebUI.Services;

namespace WebUI.Pages.Account;

public sealed class RegisterModel : PageModel
{
    private readonly IGoBikeApiClient apiClient;

    public RegisterModel(IGoBikeApiClient apiClient)
    {
        this.apiClient = apiClient;
    }

    [BindProperty]
    public CustomerRegistrationRequest Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
        => User.Identity?.IsAuthenticated == true ? RedirectToPage("/Index") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var (success, error) = await apiClient.RegisterCustomerAsync(Input);
        if (!success)
        {
            ErrorMessage = error ?? "Unable to create customer account.";
            return Page();
        }

        TempData["RegistrationSuccess"] = "Account created. You can now sign in.";
        return RedirectToPage("./Login");
    }
}
