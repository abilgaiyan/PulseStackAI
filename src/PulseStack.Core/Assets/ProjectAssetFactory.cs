using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

public sealed class ProjectAssetFactory
{
    public ProjectAsset Create(
        ProjectAssetOptions options,
        IReadOnlyCollection<AssetDependency>? dependencies = null) =>
        Create(AssetId.New(), options, dependencies);

    public ProjectAsset Create(
        AssetId id,
        ProjectAssetOptions options,
        IReadOnlyCollection<AssetDependency>? dependencies = null)
    {
        id.EnsureValid();
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);

        return new ProjectAsset(
            id,
            new AssetUrn($"urn:pulsestack:project:{id}"),
            options,
            dependencies);
    }
}
