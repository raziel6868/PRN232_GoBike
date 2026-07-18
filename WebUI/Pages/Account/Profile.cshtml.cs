using System.Security.Claims;
using BusinessObjects.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebUI.Helpers;
using WebUI.Services;

namespace WebUI.Pages.Account;

[Authorize]
public sealed class ProfileModel : PageModel
{
    private readonly IGoBikeApiClient apiClient;

    public ProfileModel(IGoBikeApiClient apiClient)
    {
        this.apiClient = apiClient;
    }

    public UserProfileDto? UserProfile { get; set; }
    public string? ErrorMessage { get; set; }
    public bool IsCustomer => User.IsInRole("Customer");

    [BindProperty]
    public CustomerProfileUpdateRequest Form { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var result = await LoadProfileAsync(populateForm: true);
        return result ?? Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!IsCustomer)
            return Forbid();

        if (!ModelState.IsValid)
        {
            await LoadProfileAsync(populateForm: false);
            return Page();
        }

        var (success, profile, error) = await apiClient.UpdateOwnProfileAsync(Form);
        var redirect = await ApiPageHelper.HandleApiAuthFailureAsync(this, error, apiClient);
        if (redirect != null)
            return redirect;

        if (!success || profile == null)
        {
            ErrorMessage = error ?? "Unable to update profile.";
            await LoadProfileAsync(populateForm: false);
            return Page();
        }

        await RefreshLocalClaimsAsync(profile);
        TempData["ProfileSuccess"] = "Profile updated successfully.";
        return RedirectToPage();
    }

    private async Task<IActionResult?> LoadProfileAsync(bool populateForm)
    {
        var (success, profile, error) = await apiClient.GetProfileAsync();
        var redirect = await ApiPageHelper.HandleApiAuthFailureAsync(this, error, apiClient);
        if (redirect != null)
            return redirect;

        if (!success || profile == null)
        {
            ErrorMessage = error ?? "Unable to load profile.";
            return null;
        }

        UserProfile = profile;
        if (populateForm && profile.RoleName == "Customer")
        {
            Form = new CustomerProfileUpdateRequest
            {
                FullName = profile.FullName,
                Email = profile.Email ?? string.Empty,
                PhoneNumber = profile.PhoneNumber ?? string.Empty,
                Address = profile.Address,
                CCCD = profile.CCCD ?? string.Empty,
                DateOfBirth = profile.DateOfBirth ?? DateTime.Today,
                DriverLicenseNo = profile.DriverLicenseNo ?? string.Empty
            };
        }

        return null;
    }

    private async Task RefreshLocalClaimsAsync(UserProfileDto profile)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, profile.Id.ToString()),
            new(ClaimTypes.Name, profile.Username),
            new(ClaimTypes.Email, profile.Email ?? string.Empty),
            new(ClaimTypes.GivenName, profile.FullName),
            new(ClaimTypes.Role, profile.RoleName)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false });
    }
}
