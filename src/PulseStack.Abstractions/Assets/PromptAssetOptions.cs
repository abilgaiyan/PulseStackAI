namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative configuration for a Prompt Asset.
/// </summary>
public sealed record PromptAssetOptions
{
    public required string Name { get; init; }

    public required string SystemInstructions { get; init; }
}
