using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

public sealed class ToolAssetFactory
{
    public ToolAsset Create(ToolAssetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Description);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Category);

        var id = AssetId.New();

        return new ToolAsset(
            id,
            new AssetUrn($"urn:pulsestack:tool:{id}"),
            options);
    }
}
