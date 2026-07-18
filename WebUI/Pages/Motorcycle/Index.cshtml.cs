using BusinessObjects.Enums;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Services.DTOs;
using WebUI.Services;

namespace WebUI.Pages.Motorcycle;

public class IndexModel : PageModel
{
    private readonly IGoBikeApiClient _apiClient;
    private readonly IWebHostEnvironment _environment;

    public IndexModel(IGoBikeApiClient apiClient, IWebHostEnvironment environment)
    {
        _apiClient = apiClient;
        _environment = environment;
    }

    public PaginatedResult<MotorcycleDto> Result { get; set; } = new();
    public List<MotorcycleTypeDto> VehicleTypes { get; set; } = [];

    [BindProperty(SupportsGet = true)] public string? Search { get; set; }
    [BindProperty(SupportsGet = true)] public MotorcycleStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MinPrice { get; set; }
    [BindProperty(SupportsGet = true)] public decimal? MaxPrice { get; set; }
    [BindProperty(SupportsGet = true, Name = "page")] public int PageNumber { get; set; } = 1;

    public async Task OnGetAsync()
    {
        var (success, result, error) = await _apiClient.GetMotorcyclesAsync(
            Search, Status, MinPrice, MaxPrice, PageNumber);

        if (success && result != null)
            Result = result;
        else if (!string.IsNullOrEmpty(error))
            TempData["Error"] = error;

        var (typesSuccess, types, typesError) = await _apiClient.GetMotorcycleTypesAsync();
        if (typesSuccess && types != null)
            VehicleTypes = types;
        else if (!string.IsNullOrEmpty(typesError) && TempData["Error"] == null)
            TempData["Error"] = typesError;
    }

    public async Task<IActionResult> OnPostCreateAsync([FromForm] CreateMotorcycleRequest request, IFormFile? imageFile)
    {
        if (!User.IsInRole("Admin"))
            return Forbid();

        var (imageUrl, imageError) = await SaveMotorcycleImageAsync(imageFile);
        if (imageError != null)
        {
            TempData["Error"] = imageError;
            return RedirectToPage();
        }

        if (imageUrl != null)
            request.ImageUrl = imageUrl;

        var (success, _, error) = await _apiClient.CreateMotorcycleAsync(request);
        TempData[success ? "Success" : "Error"] = success
            ? "Motorcycle created successfully!"
            : error ?? "Failed to create motorcycle.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditAsync(int id, [FromForm] UpdateMotorcycleRequest request, IFormFile? imageFile)
    {
        if (!User.IsInRole("Admin") && !User.IsInRole("Staff"))
            return Forbid();

        var (imageUrl, imageError) = await SaveMotorcycleImageAsync(imageFile);
        if (imageError != null)
        {
            TempData["Error"] = imageError;
            return RedirectToPage();
        }

        if (imageUrl != null)
            request.ImageUrl = imageUrl;

        var (success, _, error) = await _apiClient.UpdateMotorcycleAsync(id, request);
        TempData[success ? "Success" : "Error"] = success
            ? "Motorcycle updated successfully!"
            : error ?? "Failed to update motorcycle.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!User.IsInRole("Admin"))
            return Forbid();

        var (success, error) = await _apiClient.DeleteMotorcycleAsync(id);
        TempData[success ? "Success" : "Error"] = success
            ? "Motorcycle deleted successfully!"
            : error ?? "Failed to delete motorcycle.";
        return RedirectToPage();
    }

    private async Task<(string? ImageUrl, string? Error)> SaveMotorcycleImageAsync(IFormFile? imageFile)
    {
        if (imageFile == null || imageFile.Length == 0)
            return (null, null);

        if (imageFile.Length > 5 * 1024 * 1024)
            return (null, "Image file must be 5 MB or smaller.");

        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp", ".gif"
        };

        if (!allowedExtensions.Contains(extension))
            return (null, "Only JPG, PNG, WEBP, and GIF images are allowed.");

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "motorcycles");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadsFolder, fileName);

        await using var stream = System.IO.File.Create(physicalPath);
        await imageFile.CopyToAsync(stream);

        return ($"/uploads/motorcycles/{fileName}", null);
    }
}
