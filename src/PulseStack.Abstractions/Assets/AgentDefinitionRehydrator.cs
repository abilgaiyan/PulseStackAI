namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Reconstructs Agent definitions from already validated declarative state.
/// This boundary does not perform runtime realization or Asset resolution.
/// </summary>
internal static class AgentDefinitionRehydrator
{
    internal static AgentDefinition Rehydrate(
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        AssetMetadata metadata,
        AssetLifecycle lifecycle,
        IReadOnlyCollection<AssetDependency> dependencies,
        AgentDefinitionOptions options)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(options);

        return new AgentDefinition(id, urn, options) with
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
