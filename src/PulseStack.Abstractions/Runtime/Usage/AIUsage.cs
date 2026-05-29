namespace PulseStack.Abstractions.Runtime.Usage;

public sealed class AIUsage
{
    public string Provider { get; init; }
        = string.Empty;

    public string Model { get; init; }
        = string.Empty;

    public long PromptTokens { get; init; }

    public long CompletionTokens { get; init; }

    public long TotalTokens =>
        PromptTokens + CompletionTokens;
}
