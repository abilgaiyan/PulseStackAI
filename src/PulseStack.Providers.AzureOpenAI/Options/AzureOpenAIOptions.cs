namespace PulseStack.Providers.AzureOpenAI.Options;

public sealed class AzureOpenAIOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Deployment { get; set; } = string.Empty;
}