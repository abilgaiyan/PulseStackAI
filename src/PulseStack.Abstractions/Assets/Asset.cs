namespace PulseStack.Abstractions.Assets;

public abstract record Asset : IAsset
{
    public required AssetId Id { get; init; }

    public required AssetUrn Urn { get; init; }

    public required AssetVersion Version { get; init; }

    public required AssetMetadata Metadata { get; init; }

    public AssetType Type { get; }

    public AssetLifecycle Lifecycle { get; init; }

    public IReadOnlyCollection<AssetReference> References { get; init; } = [];

    public IReadOnlyCollection<AssetDependency> Dependencies { get; init; } = [];
}
