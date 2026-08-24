using System.Diagnostics.CodeAnalysis;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative Tool Asset describing a reusable business capability.
/// </summary>
public sealed record ToolAsset : Asset
{
    [SetsRequiredMembers]
    internal ToolAsset(
        AssetId id,
        AssetUrn urn,
        ToolAssetOptions options)
        : base(AssetType.Tool)
    {
        ArgumentNullException.ThrowIfNull(options);

        Id = id;
        Urn = urn;
        Version = AssetVersion.Initial;
        Metadata = new AssetMetadata
        {
            Name = options.Name,
            Description = options.Description,
            Category = options.Category,
            Tags = options.Tags.ToArray()
        };
        Lifecycle = AssetLifecycle.Draft;
        Options = options with
        {
            Tags = options.Tags.ToArray()
        };
    }

    public ToolAssetOptions Options { get; }
}
