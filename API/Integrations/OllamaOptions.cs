namespace API.Integrations;

public sealed class OllamaOptions
{
    public const string SectionName = "AI:Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434/";
    public string Model { get; set; } = "qwen3:0.6b";
}
