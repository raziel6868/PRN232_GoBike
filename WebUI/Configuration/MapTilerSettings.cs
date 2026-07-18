namespace WebUI.Configuration;

public sealed class MapTilerSettings
{
    public const string SectionName = "MapProviders:MapTiler";

    public string ApiKey { get; set; } = string.Empty;
    public string MapId { get; set; } = "streets-v4";

    public string? BuildStyleUrl()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            return null;

        var mapId = string.IsNullOrWhiteSpace(MapId) ? "streets-v4" : MapId.Trim();
        return $"https://api.maptiler.com/maps/{Uri.EscapeDataString(mapId)}/style.json?key={Uri.EscapeDataString(ApiKey.Trim())}";
    }
}
