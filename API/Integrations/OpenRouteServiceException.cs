namespace API.Integrations;

public sealed class OpenRouteServiceException : Exception
{
    public OpenRouteServiceException(string message) : base(message)
    {
    }
}
