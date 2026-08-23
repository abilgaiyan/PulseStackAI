using System.Diagnostics.CodeAnalysis;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative Prompt Asset describing reusable Agent instructions.
/// </summary>
public sealed record PromptAsset : Asset
{
    [SetsRequiredMembers]
    internal PromptAsset(
        AssetId id,
        AssetUrn urn,
        PromptAssetOptions options)
        : base(AssetType.Prompt)
    {
        ArgumentNullException.ThrowIfNull(options);

        Id = id;
        Urn = urn;
        Version = AssetVersion.Initial;
        Metadata = new AssetMetadata
        {
            Name = options.Name,
            Tags = []
        };
        Lifecycle = AssetLifecycle.Draft;
        Options = options;
    }

    public PromptAssetOptions Options { get; }
}
