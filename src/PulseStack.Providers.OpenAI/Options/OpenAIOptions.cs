namespace PulseStack.Providers.OpenAI.Options;

public sealed class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o-mini";
}
