using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Services.DTOs;

namespace API.Integrations;

public sealed class OpenRouteService : IOpenRouteService
{
    private readonly HttpClient httpClient;
    private readonly OpenRouteServiceOptions options;

    public OpenRouteService(HttpClient httpClient, IOptions<OpenRouteServiceOptions> options)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
    }

    public async Task<IReadOnlyList<PlaceSuggestionDto>> SearchPlacesAsync(
        string query,
        CancellationToken cancellationToken,
        double? focusLatitude = null,
        double? focusLongitude = null,
        int limit = 6)
    {
        EnsureConfigured();

        var hasFocus = focusLatitude.HasValue && focusLongitude.HasValue;
        var queryParameters = new Dictionary<string, string?>
        {
            ["text"] = query.Trim(),
            ["boundary.country"] = "VN",
            ["size"] = Math.Clamp(hasFocus ? limit * 2 : limit, 1, 20).ToString(CultureInfo.InvariantCulture)
        };

        if (hasFocus)
        {
            queryParameters["focus.point.lat"] = focusLatitude!.Value.ToString(CultureInfo.InvariantCulture);
            queryParameters["focus.point.lon"] = focusLongitude!.Value.ToString(CultureInfo.InvariantCulture);
        }

        var url = QueryHelpers.AddQueryString(
            BuildUrl("geocode/autocomplete"),
            queryParameters);

        using var request = CreateRequest(HttpMethod.Get, url);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        using var document = await ReadProviderResponseAsync(response, cancellationToken);

        if (!document.RootElement.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<PlaceSuggestionDto>();
        foreach (var feature in features.EnumerateArray())
        {
            if (!TryReadCoordinates(feature, out var longitude, out var latitude))
                continue;

            var properties = feature.TryGetProperty("properties", out var value) ? value : default;
            var label = ReadString(properties, "label");
            if (string.IsNullOrWhiteSpace(label))
                continue;

            var place = new PlaceSuggestionDto
            {
                Label = label,
                Name = ReadString(properties, "name"),
                Locality = ReadString(properties, "locality"),
                Region = ReadString(properties, "region"),
                Country = ReadString(properties, "country"),
                Latitude = latitude,
                Longitude = longitude
            };

            if (hasFocus)
            {
                place.DistanceMeters = CalculateDistanceMeters(
                    focusLatitude!.Value,
                    focusLongitude!.Value,
                    latitude,
                    longitude);
            }

            results.Add(place);
        }

        return results
            .OrderBy(place => place.DistanceMeters ?? double.MaxValue)
            .Take(Math.Clamp(limit, 1, 20))
            .ToList();
    }

    public async Task<RouteResultDto> ComputeRouteAsync(
        ComputeRouteRequestDto request,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var avoidFeatures = new List<string>();
        if (request.AvoidHighways)
            avoidFeatures.Add("highways");
        if (request.AvoidTolls)
            avoidFeatures.Add("tollways");
        if (request.AvoidFerries)
            avoidFeatures.Add("ferries");

        var body = new Dictionary<string, object>
        {
            ["coordinates"] = new[]
            {
                new[] { request.Origin!.Longitude, request.Origin.Latitude },
                new[] { request.Destination!.Longitude, request.Destination.Latitude }
            },
            ["instructions"] = true,
            ["instructions_format"] = "text",
            ["language"] = "en",
            ["units"] = "m"
        };

        if (avoidFeatures.Count > 0)
            body["options"] = new { avoid_features = avoidFeatures };

        using var providerRequest = CreateRequest(
            HttpMethod.Post,
            BuildUrl("v2/directions/driving-car/geojson"));
        providerRequest.Content = JsonContent.Create(body);

        using var response = await httpClient.SendAsync(providerRequest, cancellationToken);
        using var document = await ReadProviderResponseAsync(response, cancellationToken);

        if (!document.RootElement.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array ||
            features.GetArrayLength() == 0)
        {
            throw new OpenRouteServiceException("No route was found between the selected locations.");
        }

        var feature = features[0];
        var result = new RouteResultDto();

        if (feature.TryGetProperty("geometry", out var geometry) &&
            geometry.TryGetProperty("coordinates", out var coordinates) &&
            coordinates.ValueKind == JsonValueKind.Array)
        {
            foreach (var coordinate in coordinates.EnumerateArray())
            {
                if (coordinate.ValueKind == JsonValueKind.Array && coordinate.GetArrayLength() >= 2)
                    result.Coordinates.Add([coordinate[0].GetDouble(), coordinate[1].GetDouble()]);
            }
        }

        if (feature.TryGetProperty("properties", out var properties))
        {
            if (properties.TryGetProperty("summary", out var summary))
            {
                result.DistanceMeters = ReadDouble(summary, "distance");
                result.DurationSeconds = ReadDouble(summary, "duration");
            }

            if (properties.TryGetProperty("segments", out var segments) &&
                segments.ValueKind == JsonValueKind.Array)
            {
                foreach (var segment in segments.EnumerateArray())
                    ReadSteps(segment, result.Steps);
            }
        }

        if (document.RootElement.TryGetProperty("bbox", out var boundingBox) &&
            boundingBox.ValueKind == JsonValueKind.Array)
        {
            result.BoundingBox = boundingBox.EnumerateArray().Select(item => item.GetDouble()).ToArray();
        }

        return result;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Authorization", options.ApiKey);
        return request;
    }

    private string BuildUrl(string relativePath)
        => new Uri(new Uri(options.BaseUrl.TrimEnd('/') + "/"), relativePath).ToString();

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new InvalidOperationException("OpenRouteService API key is not configured.");
    }

    private static async Task<JsonDocument> ReadProviderResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = TryReadProviderError(content)
                ?? $"The routing provider returned HTTP {(int)response.StatusCode}.";
            throw new OpenRouteServiceException(message);
        }

        try
        {
            return JsonDocument.Parse(content);
        }
        catch (JsonException ex)
        {
            throw new OpenRouteServiceException($"The routing provider returned invalid data: {ex.Message}");
        }
    }

    private static string? TryReadProviderError(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString();
                if (error.TryGetProperty("message", out var message))
                    return message.GetString();
            }

            return root.TryGetProperty("message", out var rootMessage) ? rootMessage.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryReadCoordinates(JsonElement feature, out double longitude, out double latitude)
    {
        longitude = 0;
        latitude = 0;
        if (!feature.TryGetProperty("geometry", out var geometry) ||
            !geometry.TryGetProperty("coordinates", out var coordinates) ||
            coordinates.ValueKind != JsonValueKind.Array ||
            coordinates.GetArrayLength() < 2)
        {
            return false;
        }

        longitude = coordinates[0].GetDouble();
        latitude = coordinates[1].GetDouble();
        return true;
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static double ReadDouble(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.TryGetDouble(out var value)
            ? value
            : 0;

    private static double CalculateDistanceMeters(
        double startLatitude,
        double startLongitude,
        double endLatitude,
        double endLongitude)
    {
        const double earthRadiusMeters = 6_371_000;
        var latitudeDelta = DegreesToRadians(endLatitude - startLatitude);
        var longitudeDelta = DegreesToRadians(endLongitude - startLongitude);
        var startLatitudeRadians = DegreesToRadians(startLatitude);
        var endLatitudeRadians = DegreesToRadians(endLatitude);

        var haversine = Math.Pow(Math.Sin(latitudeDelta / 2), 2) +
                        Math.Cos(startLatitudeRadians) * Math.Cos(endLatitudeRadians) *
                        Math.Pow(Math.Sin(longitudeDelta / 2), 2);

        return earthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    private static void ReadSteps(JsonElement segment, ICollection<RouteStepDto> target)
    {
        if (!segment.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
            return;

        foreach (var step in steps.EnumerateArray())
        {
            target.Add(new RouteStepDto
            {
                Instruction = ReadString(step, "instruction") ?? "Continue",
                DistanceMeters = ReadDouble(step, "distance"),
                DurationSeconds = ReadDouble(step, "duration"),
                ManeuverType = step.TryGetProperty("type", out var type) && type.TryGetInt32(out var value)
                    ? value
                    : 0
            });
        }
    }
}
