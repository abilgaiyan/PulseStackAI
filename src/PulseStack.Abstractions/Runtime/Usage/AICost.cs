namespace PulseStack.Abstractions.Runtime.Usage;

public sealed class AICost
{
    public decimal PromptCost { get; init; }

    public decimal CompletionCost { get; init; }

    public decimal TotalCost =>
        PromptCost + CompletionCost;

    public string Currency { get; init; }
        = "USD";
}
