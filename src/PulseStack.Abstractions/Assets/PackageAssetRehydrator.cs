namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Reconstructs Package Assets from already validated declarative state.
/// This boundary restores persisted Asset state without copying the persisted common References projection.
/// </summary>
internal static class PackageAssetRehydrator
{
    internal static PackageAsset Rehydrate(
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        AssetMetadata metadata,
        AssetLifecycle lifecycle,
        IReadOnlyList<AssetDependency> dependencies,
        PackageAssetOptions options)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(options);

        return new PackageAsset(
            id,
            urn,
            version,
            options,
            dependencies) with
        {
            Metadata = metadata with
            {
                Tags = metadata.Tags.ToArray()
            },
            Lifecycle = lifecycle,
            Dependencies = dependencies.ToArray()
        };
    }
}
