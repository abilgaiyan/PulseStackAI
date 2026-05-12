namespace PulseStack.Providers.OpenRouter.Options;

public sealed class OpenRouterOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } =
        "openai/gpt-4.1-mini";

    public string Endpoint { get; set; } =
        "https://openrouter.ai/api/v1";
}