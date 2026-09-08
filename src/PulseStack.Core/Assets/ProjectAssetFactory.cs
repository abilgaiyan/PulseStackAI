using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

public sealed class ProjectAssetFactory
{
    public ProjectAsset Create(
        ProjectAssetOptions options,
        IReadOnlyCollection<AssetDependency>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);

        var id = AssetId.New();

        return new ProjectAsset(
            id,
            new AssetUrn($"urn:pulsestack:project:{id}"),
            options,
            dependencies);
    }
}
