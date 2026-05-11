namespace PulseStack.Providers.Ollama.Options;

public sealed class OllamaOptions
{
    public string Endpoint { get; set; } =
        "http://localhost:11434";

    public string Model { get; set; } =
        "llama3";
}