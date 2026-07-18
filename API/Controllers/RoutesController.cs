using API.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Services.DTOs;

namespace API.Controllers;

[ApiController]
[EnableRateLimiting("PublicRouteApi")]
[Route("api/routes")]
public sealed class RoutesController : ControllerBase
{
    private readonly IOpenRouteService routeService;
    private readonly IPlaceIntentService placeIntentService;

    public RoutesController(IOpenRouteService routeService, IPlaceIntentService placeIntentService)
    {
        this.routeService = routeService;
        this.placeIntentService = placeIntentService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 3)
            return BadRequest(new { message = "Enter at least 3 characters to search for a location." });

        if (query.Length > 200)
            return BadRequest(new { message = "The location search is too long." });

        return await ExecuteAsync(
            () => routeService.SearchPlacesAsync(query, cancellationToken),
            cancellationToken);
    }

    [HttpPost("compute")]
    public Task<IActionResult> Compute(
        [FromBody] ComputeRouteRequestDto request,
        CancellationToken cancellationToken)
        => ExecuteAsync(() => routeService.ComputeRouteAsync(request, cancellationToken), cancellationToken);

    [HttpPost("assistant")]
    public async Task<IActionResult> AskAssistant(
        [FromBody] RouteAssistantRequestDto request,
        CancellationToken cancellationToken)
    {
        var intent = await placeIntentService.ExtractAsync(request.Message, cancellationToken);
        return await ExecuteAsync(async () =>
        {
            var places = await routeService.SearchPlacesAsync(
                intent.SearchQuery,
                cancellationToken,
                request.Latitude,
                request.Longitude,
                8);

            return new RouteAssistantResponseDto
            {
                AssistantMessage = places.Count == 0
                    ? "Tôi chưa tìm thấy địa điểm phù hợp gần khu vực này. Hãy thử mô tả khác nhé."
                    : intent.AssistantMessage,
                SearchQuery = intent.SearchQuery,
                UsedAi = intent.UsedAi,
                Places = places.ToList()
            };
        }, cancellationToken);
    }

    private static async Task<IActionResult> ExecuteAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return new OkObjectResult(await action());
        }
        catch (InvalidOperationException ex)
        {
            return new ObjectResult(new { message = ex.Message }) { StatusCode = StatusCodes.Status503ServiceUnavailable };
        }
        catch (OpenRouteServiceException ex)
        {
            return new ObjectResult(new { message = ex.Message }) { StatusCode = StatusCodes.Status502BadGateway };
        }
        catch (HttpRequestException)
        {
            return new ObjectResult(new { message = "The routing provider is currently unavailable." })
            {
                StatusCode = StatusCodes.Status502BadGateway
            };
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ObjectResult(new { message = "The routing request timed out." })
            {
                StatusCode = StatusCodes.Status504GatewayTimeout
            };
        }
    }
}
