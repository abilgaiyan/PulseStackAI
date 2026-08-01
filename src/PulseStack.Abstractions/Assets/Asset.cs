
namespace PulseStack.Abstractions.Assets;
public abstract record Asset : IAsset
{
    public AssetId Id { get; init; }

    public AssetUrn Urn { get; init; }

    public AssetVersion Version { get; init; }

    public AssetMetadata Metadata { get; init; }

    public AssetType Type { get; }

    public AssetLifecycle Lifecycle { get; init; }

    public IReadOnlyCollection<AssetReference> References { get; init; }

    public IReadOnlyCollection<AssetDependency> Dependencies { get; init; }
}
