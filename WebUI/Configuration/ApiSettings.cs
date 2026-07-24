namespace WebUI.Configuration;

public class ApiSettings
{
    public const string SectionName = "ApiSettings";
    public string BaseUrl { get; set; } = "https://localhost:7144";
    public int TimeoutSeconds { get; set; } = 180;
}
