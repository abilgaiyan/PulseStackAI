using System.Diagnostics.CodeAnalysis;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative Policy Asset describing reusable governance intent.
/// </summary>
public sealed record PolicyAsset : Asset
{
    [SetsRequiredMembers]
    internal PolicyAsset(
        AssetId id,
        AssetUrn urn,
        PolicyAssetOptions options)
        : base(AssetType.Policy)
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

    public PolicyAssetOptions Options { get; }
}
