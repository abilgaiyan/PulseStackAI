using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

public sealed class KnowledgeAssetFactory
{
    public KnowledgeAsset Create(KnowledgeAssetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Description);

        var id = AssetId.New();

        return new KnowledgeAsset(
            id,
            new AssetUrn($"urn:pulsestack:knowledge:{id}"),
            options);
    }
}
