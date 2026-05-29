namespace PulseStack.Abstractions.Runtime.Usage;

public sealed class ModelPricing
{
    public string Provider { get; init; }
        = string.Empty;

    public string Model { get; init; }
        = string.Empty;

    public decimal InputPricePerMillionTokens { get; init; }

    public decimal OutputPricePerMillionTokens { get; init; }

    public string Currency { get; init; }
        = "USD";
}
