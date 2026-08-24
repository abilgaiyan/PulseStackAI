using System.Diagnostics.CodeAnalysis;

namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Declarative Memory Asset describing retained conversational context.
/// </summary>
public sealed record MemoryAsset : Asset
{
    [SetsRequiredMembers]
    internal MemoryAsset(
        AssetId id,
        AssetUrn urn,
        MemoryAssetOptions options)
        : base(AssetType.Memory)
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

    public MemoryAssetOptions Options { get; }
}
