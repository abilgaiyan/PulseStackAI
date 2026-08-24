using System.Diagnostics.CodeAnalysis;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative Knowledge Asset describing reusable business knowledge.
/// </summary>
public sealed record KnowledgeAsset : Asset
{
    [SetsRequiredMembers]
    internal KnowledgeAsset(
        AssetId id,
        AssetUrn urn,
        KnowledgeAssetOptions options)
        : base(AssetType.Knowledge)
    {
        ArgumentNullException.ThrowIfNull(options);

        Id = id;
        Urn = urn;
        Version = AssetVersion.Initial;
        Metadata = new AssetMetadata
        {
            Name = options.Name,
            Description = options.Description,
            Tags = options.Tags.ToArray()
        };
        Lifecycle = AssetLifecycle.Draft;
        Options = options with
        {
            Tags = options.Tags.ToArray()
        };
    }

    public KnowledgeAssetOptions Options { get; }
}
