namespace PulseStack.Providers.Gemini.Options;

public sealed class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } =
        "gemini-2.0-flash";

    public bool UseFunctionInvocation { get; set; } = false;

    public bool UseLogging { get; set; }

    public bool UseOpenTelemetry { get; set; }
}