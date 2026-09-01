namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Reconstructs foundation Asset definitions from already validated declarative state.
/// This boundary does not perform runtime, provider, catalog, or persistence resolution.
/// </summary>
internal static class FoundationAssetRehydrator
{
    internal static PromptAsset RehydratePrompt(
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        AssetMetadata metadata,
        AssetLifecycle lifecycle,
        IReadOnlyCollection<AssetReference> references,
        IReadOnlyCollection<AssetDependency> dependencies,
        PromptAssetOptions options)
        => new PromptAsset(id, urn, options) with
        {
            Version = version,
            Metadata = SnapshotMetadata(metadata),
            Lifecycle = lifecycle,
            References = references.ToArray(),
            Dependencies = dependencies.ToArray()
        };

    internal static ToolAsset RehydrateTool(
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        AssetMetadata metadata,
        AssetLifecycle lifecycle,
        IReadOnlyCollection<AssetReference> references,
        IReadOnlyCollection<AssetDependency> dependencies,
        ToolAssetOptions options)
        => new ToolAsset(id, urn, options) with
        {
            Version = version,
            Metadata = SnapshotMetadata(metadata),
            Lifecycle = lifecycle,
            References = references.ToArray(),
            Dependencies = dependencies.ToArray()
        };

    internal static KnowledgeAsset RehydrateKnowledge(
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        AssetMetadata metadata,
        AssetLifecycle lifecycle,
        IReadOnlyCollection<AssetReference> references,
        IReadOnlyCollection<AssetDependency> dependencies,
        KnowledgeAssetOptions options)
        => new KnowledgeAsset(id, urn, options) with
        {
            Version = version,
            Metadata = SnapshotMetadata(metadata),
            Lifecycle = lifecycle,
            References = references.ToArray(),
            Dependencies = dependencies.ToArray()
        };

    internal static MemoryAsset RehydrateMemory(
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        AssetMetadata metadata,
        AssetLifecycle lifecycle,
        IReadOnlyCollection<AssetReference> references,
        IReadOnlyCollection<AssetDependency> dependencies,
        MemoryAssetOptions options)
        => new MemoryAsset(id, urn, options) with
        {
            Version = version,
            Metadata = SnapshotMetadata(metadata),
            Lifecycle = lifecycle,
            References = references.ToArray(),
            Dependencies = dependencies.ToArray()
        };

    internal static PolicyAsset RehydratePolicy(
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        AssetMetadata metadata,
        AssetLifecycle lifecycle,
        IReadOnlyCollection<AssetReference> references,
        IReadOnlyCollection<AssetDependency> dependencies,
        PolicyAssetOptions options)
        => new PolicyAsset(id, urn, options) with
        {
            Version = version,
            Metadata = SnapshotMetadata(metadata),
            Lifecycle = lifecycle,
            References = references.ToArray(),
            Dependencies = dependencies.ToArray()
        };

    internal static ModelAsset RehydrateModel(
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        AssetMetadata metadata,
        AssetLifecycle lifecycle,
        IReadOnlyCollection<AssetReference> references,
        IReadOnlyCollection<AssetDependency> dependencies,
        ModelAssetOptions options)
        => new ModelAsset(id, urn, options) with
        {
            Version = version,
            Metadata = SnapshotMetadata(metadata),
            Lifecycle = lifecycle,
            References = references.ToArray(),
            Dependencies = dependencies.ToArray()
        };

    private static AssetMetadata SnapshotMetadata(AssetMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata with
        {
            Tags = metadata.Tags.ToArray()
        };
    }
}
