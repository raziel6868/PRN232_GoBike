using System.Security.Claims;
using BusinessObjects.DTOs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebUI.Helpers;
using WebUI.Services;

namespace WebUI.Pages.Account;

public class LoginModel : PageModel
{
    private readonly IGoBikeApiClient apiClient;
    private readonly IApiCookieAccessor apiCookieAccessor;
    private readonly ILogger<LoginModel> logger;

    public LoginModel(
        IGoBikeApiClient apiClient,
        IApiCookieAccessor apiCookieAccessor,
        ILogger<LoginModel> logger)
    {
        this.apiClient = apiClient;
        this.apiCookieAccessor = apiCookieAccessor;
        this.logger = logger;
    }

    [BindProperty]
    public LoginRequest Input { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true && HasValidApiSession())
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return AuthRedirectHelper.RedirectToHome(this);
        }

        if (User.Identity?.IsAuthenticated == true)
            await SignOutLocalAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(Input.Username) || string.IsNullOrWhiteSpace(Input.Password))
        {
            ErrorMessage = "Username and password are required";
            return Page();
        }

        var (success, user, error) = await apiClient.LoginAsync(Input);
        if (!success || user == null)
        {
            ErrorMessage = error ?? "Invalid username or password";
            logger.LogWarning("Failed login attempt for username: {Username}", Input.Username);
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email ?? ""),
            new(ClaimTypes.GivenName, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties { IsPersistent = false });

        logger.LogInformation("User {Username} logged in via API", user.Username);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        if (user.Role == BusinessObjects.Enums.UserRole.Customer)
            return RedirectToPage("/Motorcycle/Index");

        return AuthRedirectHelper.RedirectToHome(this);
    }

    private bool HasValidApiSession()
        => !string.IsNullOrEmpty(apiCookieAccessor.GetCookieHeader());

    private async Task SignOutLocalAsync()
    {
        await apiClient.LogoutAsync();
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
