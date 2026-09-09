namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative configuration for a Package Asset representing one distribution boundary.
/// </summary>
public sealed record PackageAssetOptions
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyList<AssetReference> Members { get; init; } = [];
}
