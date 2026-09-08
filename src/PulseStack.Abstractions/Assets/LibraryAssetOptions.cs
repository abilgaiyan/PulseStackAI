namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative configuration for a Library Asset representing one reusable collection of AI Asset definitions.
/// </summary>
public sealed record LibraryAssetOptions
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public IReadOnlyCollection<AssetReference> Members { get; init; } = [];
}
