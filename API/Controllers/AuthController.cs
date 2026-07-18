using System.Security.Claims;
using BusinessObjects.DTOs;
using API.Accounts;
using BusinessObjects.Entities;
using BusinessObjects.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserService userService;
    private readonly ICustomerAccountService customerAccountService;

    public AuthController(IUserService userService, ICustomerAccountService customerAccountService)
    {
        this.userService = userService;
        this.customerAccountService = customerAccountService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Username and password are required" });

        var user = await userService.GetByUsernameAsync(request.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid username or password" });

        if (!user.IsActive)
            return Unauthorized(new { message = "User account is inactive" });

        if (user.Role == UserRole.Customer && (user.Customer == null || !user.Customer.IsActive))
            return Unauthorized(new { message = "Customer account is inactive" });

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.GivenName, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties
            {
                IsPersistent = false
            });

        return Ok(new ApiLoginResult
        {
            Message = "Login successful",
            User = MapLoginResponse(user)
        });
    }

    [AllowAnonymous]
    [EnableRateLimiting("Registration")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] CustomerRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var (user, error) = await customerAccountService.RegisterAsync(request, cancellationToken);
        if (user == null)
            return Conflict(new { message = error ?? "Unable to register customer account." });

        return Created("/api/auth/profile", new ApiLoginResult
        {
            Message = "Customer account created successfully",
            User = MapLoginResponse(user)
        });
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { message = "Logout successful" });
    }

    [AllowAnonymous]
    [HttpGet("access-denied")]
    public IActionResult AccessDenied()
    {
        return StatusCode(StatusCodes.Status403Forbidden, new { message = "Access denied" });
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var user = await customerAccountService.GetUserWithCustomerAsync(userId, cancellationToken);
        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(MapProfile(user));
    }

    [Authorize(Roles = "Customer")]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] CustomerProfileUpdateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var (user, error) = await customerAccountService.UpdateOwnProfileAsync(
            userId,
            request,
            cancellationToken);

        return user == null
            ? BadRequest(new { message = error ?? "Unable to update customer profile." })
            : Ok(MapProfile(user));
    }

    private static LoginResponse MapLoginResponse(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        FullName = user.FullName,
        Email = user.Email ?? string.Empty,
        Role = user.Role,
        CustomerId = user.CustomerId
    };

    private static UserProfileDto MapProfile(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        FullName = user.Customer?.FullName ?? user.FullName,
        Email = user.Customer?.Email ?? user.Email,
        PhoneNumber = user.Customer?.PhoneNumber ?? user.PhoneNumber,
        Role = user.Role,
        CustomerId = user.CustomerId,
        Address = user.Customer?.Address,
        CCCD = user.Customer?.CCCD,
        DateOfBirth = user.Customer?.DateOfBirth,
        DriverLicenseNo = user.Customer?.DriverLicenseNo,
        IsActive = user.IsActive && (user.Customer?.IsActive ?? true),
        CreatedAt = user.CreatedAt
    };

    private bool TryGetUserId(out int userId)
        => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);
}
