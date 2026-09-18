using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

/// <summary>
/// Creates Prompt Assets from declarative Prompt options.
/// </summary>
public sealed class PromptAssetFactory
{
    public PromptAsset Create(PromptAssetOptions options) =>
        Create(AssetId.New(), options);

    public PromptAsset Create(AssetId id, PromptAssetOptions options)
    {
        id.EnsureValid();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.SystemInstructions);

        return new PromptAsset(
            id,
            new AssetUrn($"urn:pulsestack:prompt:{id}"),
            options);
    }
}
