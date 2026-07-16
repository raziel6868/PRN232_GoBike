using Microsoft.AspNetCore.Http;

namespace WebUI.Helpers;

public static class DepositProofImageHelper
{
    private const long MaxFileSize = 5 * 1024 * 1024;

    public static async Task<(string? ImageUrl, string? Error)> SaveAsync(
        IWebHostEnvironment environment,
        IFormFile? imageFile)
    {
        if (imageFile is null || imageFile.Length == 0)
            return (null, "A deposit proof image is required.");

        if (imageFile.Length > MaxFileSize)
            return (null, "Deposit proof image must be 5 MB or smaller.");

        var extension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".webp"
        };

        if (!allowedExtensions.Contains(extension))
            return (null, "Deposit proof image must be a JPG, PNG, or WEBP file.");

        var uploadFolder = Path.Combine(environment.WebRootPath, "uploads", "rental-deposits");
        Directory.CreateDirectory(uploadFolder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(uploadFolder, fileName);
        await using var stream = File.Create(physicalPath);
        await imageFile.CopyToAsync(stream);

        return ($"/uploads/rental-deposits/{fileName}", null);
    }
}
