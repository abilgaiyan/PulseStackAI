namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative configuration for a reusable Policy Asset.
/// </summary>
public sealed record PolicyAssetOptions
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyCollection<string> Tags { get; init; } = [];
}
