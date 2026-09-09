using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

public sealed class PackageAssetFactory
{
    public PackageAsset Create(
        PackageAssetOptions options,
        IReadOnlyList<AssetDependency>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);

        var id = AssetId.New();

        return new PackageAsset(
            id,
            new AssetUrn($"urn:pulsestack:package:{id}"),
            options,
            dependencies);
    }
}
