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
    public bool IsStaff => User.IsInRole("Staff");
    public bool IsAdmin => User.IsInRole("Admin");
    public bool IsInternalUser => IsStaff || IsAdmin;

    [BindProperty]
    public CustomerProfileUpdateRequest Form { get; set; } = new();

    [BindProperty]
    public InternalProfileUpdateRequest InternalForm { get; set; } = new();

    [BindProperty]
    public ChangePasswordRequest PasswordForm { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var result = await LoadProfileAsync(populateForm: true);
        return result ?? Page();
    }

    public async Task<IActionResult> OnPostProfileAsync()
    {
        if (!IsCustomer && !IsInternalUser)
            return Forbid();

        if (IsInternalUser)
        {
            ModelState.Clear();
            if (!TryValidateModel(InternalForm, nameof(InternalForm)))
            {
                await LoadProfileAsync(populateForm: false);
                return Page();
            }

            var (internalSuccess, internalProfile, internalError) =
                await apiClient.UpdateInternalProfileAsync(InternalForm);
            var internalRedirect = await ApiPageHelper.HandleApiAuthFailureAsync(this, internalError, apiClient);
            if (internalRedirect != null)
                return internalRedirect;

            if (!internalSuccess || internalProfile == null)
            {
                ErrorMessage = internalError ?? "Unable to update profile.";
                await LoadProfileAsync(populateForm: false);
                return Page();
            }

            await RefreshLocalClaimsAsync(internalProfile);
            TempData["ProfileSuccess"] = "Profile updated successfully.";
            return RedirectToPage();
        }

        ModelState.Clear();
        if (!TryValidateModel(Form, nameof(Form)))
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

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(PasswordForm, nameof(PasswordForm)))
        {
            await LoadProfileAsync(populateForm: true);
            return Page();
        }

        var (success, error) = await apiClient.ChangePasswordAsync(PasswordForm);
        var redirect = await ApiPageHelper.HandleApiAuthFailureAsync(this, error, apiClient);
        if (redirect != null)
            return redirect;

        if (!success)
        {
            ErrorMessage = error ?? "Unable to change password.";
            await LoadProfileAsync(populateForm: true);
            return Page();
        }

        TempData["PasswordSuccess"] = "Password changed successfully.";
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
        else if (populateForm && profile.RoleName is "Staff" or "Admin")
        {
            InternalForm = new InternalProfileUpdateRequest
            {
                FullName = profile.FullName,
                Email = profile.Email ?? string.Empty
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
