namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative configuration for a Project Asset representing one intelligent application.
/// </summary>
public sealed record ProjectAssetOptions
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required AssetReference EntryWorkflow { get; init; }

    public IReadOnlyList<AssetReference> OwnedAssets { get; init; } = [];
}
