using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

/// <summary>
/// Creates Prompt Assets from declarative Prompt options.
/// </summary>
public sealed class PromptAssetFactory
{
    public PromptAsset Create(PromptAssetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SystemInstructions);

        var id = AssetId.New();

        return new PromptAsset(
            id,
            new AssetUrn($"urn:pulsestack:prompt:{id}"),
            options);
    }
}
