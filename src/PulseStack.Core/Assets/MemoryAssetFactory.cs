using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

public sealed class MemoryAssetFactory
{
    public MemoryAsset Create(MemoryAssetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Description);

        var id = AssetId.New();

        return new MemoryAsset(
            id,
            new AssetUrn($"urn:pulsestack:memory:{id}"),
            options);
    }
}
