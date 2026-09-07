namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Reconstructs Workflow Assets from already validated declarative state.
/// This boundary restores persisted Asset state without performing resolution or runtime realization.
/// </summary>
internal static class WorkflowAssetRehydrator
{
    internal static WorkflowAsset Rehydrate(
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        AssetMetadata metadata,
        AssetLifecycle lifecycle,
        IReadOnlyCollection<AssetDependency> dependencies,
        WorkflowAssetOptions options)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(options);

        return new WorkflowAsset(id, urn, options) with
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
