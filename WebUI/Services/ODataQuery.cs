using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Services.DTOs;

namespace WebUI.Services;

public sealed class ODataResponse<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = [];

    [JsonPropertyName("@odata.count")]
    public int? Count { get; set; }
}

public static class ODataQuery
{
    public static string BuildCollectionUrl(
        string entitySet,
        IEnumerable<string> filters,
        string orderBy,
        int pageNumber,
        int pageSize)
    {
        var normalizedPage = Math.Max(1, pageNumber);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var filter = string.Join(" and ", filters.Where(value => !string.IsNullOrWhiteSpace(value)));

        var query = new Dictionary<string, string?>
        {
            ["$count"] = "true",
            ["$top"] = normalizedPageSize.ToString(CultureInfo.InvariantCulture),
            ["$skip"] = ((normalizedPage - 1) * normalizedPageSize).ToString(CultureInfo.InvariantCulture),
            ["$orderby"] = orderBy
        };

        if (!string.IsNullOrWhiteSpace(filter))
            query["$filter"] = filter;

        return QueryHelpers.AddQueryString($"/odata/{entitySet}", query);
    }

    public static string ContainsAny(string value, params string[] properties)
    {
        var term = StringLiteral(value.Trim().ToLowerInvariant());
        return "(" + string.Join(" or ", properties.Select(property =>
            $"contains(tolower({property}), {term})")) + ")";
    }

    public static string DecimalLiteral(decimal value)
        => value.ToString(CultureInfo.InvariantCulture) + "M";

    public static string DateTimeOffsetLiteral(DateTime value)
    {
        var utcValue = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return utcValue.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    }

    private static string StringLiteral(string value)
        => $"'{value.Replace("'", "''")}'";
}

public static class ODataResponseExtensions
{
    public static PaginatedResult<T> ToPaginatedResult<T>(this ODataResponse<T> response, int pageNumber, int pageSize)
        => new()
        {
            Items = response.Value,
            CurrentPage = Math.Max(1, pageNumber),
            PageSize = Math.Clamp(pageSize, 1, 100),
            TotalItems = response.Count ?? response.Value.Count
        };
}
