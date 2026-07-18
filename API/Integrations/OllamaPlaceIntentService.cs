using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace API.Integrations;

public sealed class OllamaPlaceIntentService : IPlaceIntentService
{
    private const string SystemPrompt = """
        Extract a nearby-place intent for a map in Vietnam.
        searchQuery must be a short English geocoder phrase, for example: Vietnamese food -> "Vietnamese restaurant", ca phe -> "cafe", cay xang -> "gas station".
        assistantMessage must be one friendly sentence in the user's language.
        For unrelated requests use "point of interest". Never include coordinates or directions.
        """;

    private static readonly (string[] Terms, string Query)[] FallbackIntents =
    [
        (["do viet", "mon viet", "quan an viet", "nha hang viet"], "Vietnamese restaurant"),
        (["ca phe", "cafe", "coffee"], "cafe"),
        (["tram xang", "cay xang"], "gas station"),
        (["benh vien"], "hospital"),
        (["nha thuoc", "hieu thuoc"], "pharmacy"),
        (["khach san"], "hotel"),
        (["sieu thi"], "supermarket"),
        (["cua hang tien loi"], "convenience store"),
        (["ngan hang"], "bank"),
        (["atm"], "ATM"),
        (["bai do xe", "cho do xe"], "parking"),
        (["truong hoc"], "school")
    ];

    private readonly HttpClient httpClient;
    private readonly OllamaOptions options;
    private readonly ILogger<OllamaPlaceIntentService> logger;

    public OllamaPlaceIntentService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaPlaceIntentService> logger)
    {
        this.httpClient = httpClient;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<PlaceIntentResult> ExtractAsync(
        string message,
        CancellationToken cancellationToken)
    {
        var (fallback, hasLocalMatch) = CreateFallback(message);
        if (string.IsNullOrWhiteSpace(options.BaseUrl) || string.IsNullOrWhiteSpace(options.Model))
            return fallback;

        try
        {
            var responseSchema = new
            {
                type = "object",
                properties = new
                {
                    searchQuery = new
                    {
                        type = "string",
                        description = "A short English geocoder phrase."
                    },
                    assistantMessage = new
                    {
                        type = "string",
                        description = "One friendly sentence in the user's language."
                    }
                },
                required = new[] { "searchQuery", "assistantMessage" },
                additionalProperties = false
            };

            var body = new
            {
                model = options.Model,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = message.Trim() }
                },
                format = responseSchema,
                stream = false,
                think = false,
                keep_alive = "15m",
                options = new { temperature = 0, num_predict = 100 }
            };

            using var response = await httpClient.PostAsJsonAsync(
                BuildUrl("api/chat"),
                body,
                cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Ollama intent extraction returned HTTP {StatusCode}. Ensure model {Model} is installed.",
                    (int)response.StatusCode,
                    options.Model);
                return fallback;
            }

            var result = TryReadResult(content);
            if (result is null)
                return fallback;

            return hasLocalMatch
                ? result with { SearchQuery = fallback.SearchQuery }
                : result;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException or TaskCanceledException &&
            !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Ollama is unavailable; using local intent matching.");
            return fallback;
        }
    }

    private string BuildUrl(string relativePath)
        => new Uri(new Uri(options.BaseUrl.TrimEnd('/') + "/"), relativePath).ToString();

    private static PlaceIntentResult? TryReadResult(string content)
    {
        using var document = JsonDocument.Parse(content);
        if (!document.RootElement.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var contentElement) ||
            contentElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        using var intent = JsonDocument.Parse(contentElement.GetString() ?? string.Empty);
        var root = intent.RootElement;
        var query = root.GetProperty("searchQuery").GetString()?.Trim();
        var assistantMessage = root.GetProperty("assistantMessage").GetString()?.Trim();
        return !string.IsNullOrWhiteSpace(query) && !string.IsNullOrWhiteSpace(assistantMessage)
            ? new PlaceIntentResult(query, assistantMessage, true)
            : null;
    }

    private static (PlaceIntentResult Result, bool HasLocalMatch) CreateFallback(string message)
    {
        var normalized = RemoveDiacritics(message).ToLowerInvariant();
        var query = FallbackIntents
            .FirstOrDefault(intent => intent.Terms.Any(normalized.Contains))
            .Query;

        var hasLocalMatch = !string.IsNullOrWhiteSpace(query);
        query = string.IsNullOrWhiteSpace(query) ? message.Trim() : query;
        return (
            new PlaceIntentResult(
                query,
                "Đây là các địa điểm phù hợp gần khu vực bạn chọn.",
                false),
            hasLocalMatch);
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character == 'đ' ? 'd' : character == 'Đ' ? 'D' : character);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
