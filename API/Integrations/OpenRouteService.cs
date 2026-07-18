using System.Net.Http.Json;
using System.Globalization;
using System.Text;
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
        var normalizedLimit = Math.Clamp(limit, 1, 20);
        var nearbyRadiusMeters = Math.Clamp(options.NearbySearchRadiusMeters, 1_000, 50_000);

        if (hasFocus && TryGetPoiCategoryIds(query, out var categoryIds))
        {
            try
            {
                var poiResults = await SearchNearbyPoisAsync(
                    categoryIds,
                    focusLatitude!.Value,
                    focusLongitude!.Value,
                    nearbyRadiusMeters,
                    normalizedLimit,
                    cancellationToken);

                if (poiResults.Count > 0)
                    return poiResults;
            }
            catch (OpenRouteServiceException)
            {
                // Some ORS plans do not expose POI search, so retain bounded geocoding as a fallback.
            }
        }

        var queryParameters = new Dictionary<string, string?>
        {
            ["text"] = query.Trim(),
            ["boundary.country"] = "VN",
            ["size"] = Math.Clamp(hasFocus ? normalizedLimit * 3 : normalizedLimit, 1, 20)
                .ToString(CultureInfo.InvariantCulture)
        };

        if (hasFocus)
        {
            queryParameters["focus.point.lat"] = focusLatitude!.Value.ToString(CultureInfo.InvariantCulture);
            queryParameters["focus.point.lon"] = focusLongitude!.Value.ToString(CultureInfo.InvariantCulture);
            queryParameters["boundary.circle.lat"] = focusLatitude.Value.ToString(CultureInfo.InvariantCulture);
            queryParameters["boundary.circle.lon"] = focusLongitude.Value.ToString(CultureInfo.InvariantCulture);
            queryParameters["boundary.circle.radius"] = (nearbyRadiusMeters / 1000d)
                .ToString(CultureInfo.InvariantCulture);
        }

        var url = QueryHelpers.AddQueryString(
            BuildUrl(hasFocus ? "geocode/search" : "geocode/autocomplete"),
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
            .Where(place => !hasFocus || place.DistanceMeters <= nearbyRadiusMeters)
            .OrderBy(place => place.DistanceMeters ?? double.MaxValue)
            .Take(normalizedLimit)
            .ToList();
    }

    private async Task<IReadOnlyList<PlaceSuggestionDto>> SearchNearbyPoisAsync(
        int[] categoryIds,
        double latitude,
        double longitude,
        int radiusMeters,
        int limit,
        CancellationToken cancellationToken)
    {
        const int maximumPoiBufferMeters = 2_000;
        var poiBufferMeters = Math.Min(radiusMeters, maximumPoiBufferMeters);
        var body = new
        {
            request = "pois",
            geometry = new
            {
                geojson = new
                {
                    type = "Point",
                    coordinates = new[] { longitude, latitude }
                },
                buffer = poiBufferMeters
            },
            filters = new { category_ids = categoryIds },
            limit = Math.Clamp(limit * 5, 10, 100),
            sortby = "distance"
        };

        using var providerRequest = CreateRequest(HttpMethod.Post, BuildUrl("pois"));
        providerRequest.Content = JsonContent.Create(body);
        using var response = await httpClient.SendAsync(providerRequest, cancellationToken);
        using var document = await ReadProviderResponseAsync(response, cancellationToken);

        if (!document.RootElement.TryGetProperty("features", out var features) ||
            features.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<PlaceSuggestionDto>();
        foreach (var feature in features.EnumerateArray())
        {
            if (!TryReadCoordinates(feature, out var resultLongitude, out var resultLatitude))
                continue;

            var distance = CalculateDistanceMeters(latitude, longitude, resultLatitude, resultLongitude);
            if (distance > radiusMeters)
                continue;

            var properties = feature.TryGetProperty("properties", out var value) ? value : default;
            var tags = properties.ValueKind == JsonValueKind.Object &&
                       properties.TryGetProperty("osm_tags", out var osmTags)
                ? osmTags
                : default;
            var name = ReadString(tags, "name") ?? ReadPoiCategoryName(properties) ?? "Place";
            var locality = ReadString(tags, "addr:city") ?? ReadString(tags, "addr:district");
            var region = ReadString(tags, "addr:province") ?? ReadString(tags, "addr:state");

            results.Add(new PlaceSuggestionDto
            {
                Name = name,
                Label = BuildPoiLabel(name, tags, locality),
                Locality = locality,
                Region = region,
                Country = "Vietnam",
                Latitude = resultLatitude,
                Longitude = resultLongitude,
                DistanceMeters = distance
            });
        }

        return results
            .OrderBy(place => place.DistanceMeters)
            .Take(limit)
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

        var longitudeElement = coordinates[0];
        var latitudeElement = coordinates[1];
        return longitudeElement.ValueKind == JsonValueKind.Number &&
               latitudeElement.ValueKind == JsonValueKind.Number &&
               longitudeElement.TryGetDouble(out longitude) &&
               latitudeElement.TryGetDouble(out latitude) &&
               double.IsFinite(longitude) &&
               double.IsFinite(latitude) &&
               longitude is >= -180 and <= 180 &&
               latitude is >= -90 and <= 90;
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryGetPoiCategoryIds(string query, out int[] categoryIds)
    {
        var normalized = NormalizeForMatching(query);

        if (ContainsAny(normalized, "fast food", "do an nhanh"))
            categoryIds = [566];
        else if (ContainsAny(normalized, "cafe", "coffee", "ca phe"))
            categoryIds = [564];
        else if (ContainsAny(normalized, "restaurant", "restaurants", "quan an", "nha hang", "food", "mon viet", "vietnamese"))
            categoryIds = [570];
        else if (ContainsAny(normalized, "pharmacy", "nha thuoc"))
            categoryIds = [208];
        else if (ContainsAny(normalized, "hospital", "benh vien"))
            categoryIds = [206];
        else if (ContainsAny(normalized, "clinic", "phong kham"))
            categoryIds = [202];
        else if (ContainsAny(normalized, "atm"))
            categoryIds = [191];
        else if (ContainsAny(normalized, "bank", "ngan hang"))
            categoryIds = [192];
        else if (ContainsAny(normalized, "fuel", "gas station", "cay xang", "tram xang"))
            categoryIds = [596];
        else if (ContainsAny(normalized, "parking", "bai do xe"))
            categoryIds = [601];
        else if (ContainsAny(normalized, "hotel", "khach san"))
            categoryIds = [108];
        else if (ContainsAny(normalized, "museum", "bao tang"))
            categoryIds = [134];
        else if (ContainsAny(normalized, "park", "cong vien"))
            categoryIds = [280];
        else if (ContainsAny(normalized, "pub"))
            categoryIds = [569];
        else if (ContainsAny(normalized, "bar"))
            categoryIds = [561];
        else
        {
            categoryIds = [];
            return false;
        }

        return true;
    }

    private static bool ContainsAny(string value, params string[] terms)
        => terms.Any(value.Contains);

    private static string NormalizeForMatching(string value)
    {
        var decomposed = value.Trim().Normalize(NormalizationForm.FormD);
        var withoutMarks = string.Concat(decomposed.Where(character =>
            CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark));

        return withoutMarks
            .Normalize(NormalizationForm.FormC)
            .ToLowerInvariant()
            .Replace('đ', 'd');
    }

    private static string? ReadPoiCategoryName(JsonElement properties)
    {
        if (properties.ValueKind != JsonValueKind.Object ||
            !properties.TryGetProperty("category_ids", out var categories) ||
            categories.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var category in categories.EnumerateObject())
        {
            var categoryName = ReadString(category.Value, "category_name");
            if (!string.IsNullOrWhiteSpace(categoryName))
                return categoryName;
        }

        return null;
    }

    private static string BuildPoiLabel(string name, JsonElement tags, string? locality)
    {
        var street = ReadString(tags, "addr:street");
        var houseNumber = ReadString(tags, "addr:housenumber");
        var streetAddress = string.Join(" ", new[] { houseNumber, street }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

        return string.Join(", ", new[]
            {
                name,
                streetAddress,
                ReadString(tags, "addr:suburb"),
                locality
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static double ReadDouble(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object &&
           element.TryGetProperty(propertyName, out var property) &&
           property.ValueKind == JsonValueKind.Number &&
           property.TryGetDouble(out var value)
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
