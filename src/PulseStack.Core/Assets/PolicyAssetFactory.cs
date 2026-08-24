using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

public sealed class PolicyAssetFactory
{
    public PolicyAsset Create(PolicyAssetOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Description);

        var id = AssetId.New();

        return new PolicyAsset(
            id,
            new AssetUrn($"urn:pulsestack:policy:{id}"),
            options);
    }
}
