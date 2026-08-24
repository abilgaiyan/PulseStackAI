namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative configuration for a reusable Tool Asset.
/// </summary>
public sealed record ToolAssetOptions
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string Category { get; init; }

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}
