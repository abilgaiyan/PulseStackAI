namespace PulseStack.Providers.Ollama.Options;

public sealed class OllamaOptions
{
    public string Endpoint { get; set; } =
        "http://localhost:11434";

    public string Model { get; set; } =
        "llama3";

    public string ApiKey { get; set; } = string.Empty;

    public IReadOnlyCollection<string> AvailableModels { get; set; } = [];
}