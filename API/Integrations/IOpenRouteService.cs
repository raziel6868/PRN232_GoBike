using Services.DTOs;

namespace API.Integrations;

public interface IOpenRouteService
{
    Task<IReadOnlyList<PlaceSuggestionDto>> SearchPlacesAsync(
        string query,
        CancellationToken cancellationToken,
        double? focusLatitude = null,
        double? focusLongitude = null,
        int limit = 6);
    Task<RouteResultDto> ComputeRouteAsync(ComputeRouteRequestDto request, CancellationToken cancellationToken);
}
