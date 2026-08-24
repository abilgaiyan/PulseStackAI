namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative configuration for a reusable Memory Asset.
/// </summary>
public sealed record MemoryAssetOptions
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}
