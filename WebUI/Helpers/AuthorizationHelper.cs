using System.Security.Claims;
using BusinessObjects.Enums;

namespace WebUI.Helpers;

public static class AuthorizationHelper
{
    public static UserRole GetUserRole(ClaimsPrincipal user)
    {
        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;
        return Enum.TryParse<UserRole>(roleClaim, out var role) ? role : UserRole.Staff;
    }

    public static string GetLayoutPath(ClaimsPrincipal user)
    {
        var role = GetUserRole(user);
        return role == UserRole.Admin ? "_LayoutAdmin" : "_LayoutStaff";
    }

    public static bool IsAdmin(ClaimsPrincipal user)
    {
        return GetUserRole(user) == UserRole.Admin;
    }

    public static bool IsStaff(ClaimsPrincipal user)
    {
        return GetUserRole(user) == UserRole.Staff;
    }

    public static int GetUserId(ClaimsPrincipal user)
    {
        var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(userIdClaim, out var userId) ? userId : 0;
    }
}
