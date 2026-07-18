using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WebUI.Helpers;

public static class AuthRedirectHelper
{
    public static IActionResult RedirectToHome(PageModel page)
        => page.User.IsInRole("Customer")
            ? page.RedirectToPage("/Motorcycle/Index")
            : page.RedirectToPage("/Index");
}
