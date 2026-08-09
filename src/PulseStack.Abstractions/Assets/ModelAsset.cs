using System.Diagnostics.CodeAnalysis;

namespace PulseStack.Abstractions.Assets;

public sealed record ModelAsset : Asset
{
    [SetsRequiredMembers]
    internal ModelAsset(
        AssetId id,
        AssetUrn urn,
        ModelAssetOptions options)
        : base(AssetType.Model)
    {
        ArgumentNullException.ThrowIfNull(options);

        Id = id;
        Urn = urn;
        Version = AssetVersion.Initial;
        Metadata = new AssetMetadata
        {
            Name = options.Model,
            Tags = []
        };
        Lifecycle = AssetLifecycle.Draft;
        Options = options;
    }

    public ModelAssetOptions Options { get; }
}
