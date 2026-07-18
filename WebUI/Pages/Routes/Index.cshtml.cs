using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Services.DTOs;
using WebUI.Configuration;
using WebUI.Services;

namespace WebUI.Pages.Routes;

[Authorize(Roles = "Admin,Staff")]
public sealed class IndexModel : PageModel
{
    private readonly IGoBikeApiClient apiClient;

    public IndexModel(IGoBikeApiClient apiClient, IOptions<MapTilerSettings> mapTilerSettings)
    {
        this.apiClient = apiClient;
        MapStyleUrl = mapTilerSettings.Value.BuildStyleUrl();
    }

    public string? MapStyleUrl { get; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnGetSearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
            return BadRequest(new { message = "Enter at least 3 characters." });

        var (success, places, error) = await apiClient.SearchPlacesAsync(query.Trim());
        return success
            ? new JsonResult(places ?? [])
            : StatusCode(StatusCodes.Status502BadGateway, new { message = error ?? "Location search failed." });
    }

    public async Task<IActionResult> OnPostComputeAsync([FromBody] ComputeRouteRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Select a valid origin and destination." });

        var (success, route, error) = await apiClient.ComputeRouteAsync(request);
        return success && route != null
            ? new JsonResult(route)
            : StatusCode(StatusCodes.Status502BadGateway, new { message = error ?? "Route calculation failed." });
    }

    public async Task<IActionResult> OnPostAssistantAsync([FromBody] RouteAssistantRequestDto request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new { message = "Enter a valid place request." });

        var (success, response, error) = await apiClient.AskRouteAssistantAsync(request);
        return success && response != null
            ? new JsonResult(response)
            : StatusCode(StatusCodes.Status502BadGateway, new { message = error ?? "The route assistant is unavailable." });
    }
}
