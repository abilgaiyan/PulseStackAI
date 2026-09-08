namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Reconstructs Project Assets from already validated declarative state.
/// This boundary restores persisted Asset state without copying the persisted common References projection.
/// </summary>
internal static class ProjectAssetRehydrator
{
    internal static ProjectAsset Rehydrate(
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        AssetMetadata metadata,
        AssetLifecycle lifecycle,
        IReadOnlyCollection<AssetDependency> dependencies,
        ProjectAssetOptions options)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(options);

        return new ProjectAsset(id, urn, options, dependencies) with
        {
            Version = version,
            Metadata = metadata with
            {
                Tags = metadata.Tags.ToArray()
            },
            Lifecycle = lifecycle,
            Dependencies = dependencies.ToArray()
        };
    }
}
