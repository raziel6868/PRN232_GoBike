namespace API.Integrations;

public sealed class OpenRouteServiceOptions
{
    public const string SectionName = "MapProviders:OpenRouteService";

    public string BaseUrl { get; set; } = "https://api.openrouteservice.org/";
    public string ApiKey { get; set; } = string.Empty;
    public int NearbySearchRadiusMeters { get; set; } = 15_000;
}
