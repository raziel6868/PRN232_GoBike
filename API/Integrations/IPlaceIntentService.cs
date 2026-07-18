namespace API.Integrations;

public interface IPlaceIntentService
{
    Task<PlaceIntentResult> ExtractAsync(string message, CancellationToken cancellationToken);
}

public sealed record PlaceIntentResult(
    string SearchQuery,
    string AssistantMessage,
    bool UsedAi);
