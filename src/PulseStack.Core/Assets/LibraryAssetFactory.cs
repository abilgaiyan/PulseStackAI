using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Assets;

public sealed class LibraryAssetFactory
{
    public LibraryAsset Create(
        LibraryAssetOptions options,
        IReadOnlyCollection<AssetDependency>? dependencies = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Name);

        var id = AssetId.New();

        return new LibraryAsset(
            id,
            new AssetUrn($"urn:pulsestack:library:{id}"),
            options,
            dependencies);
    }
}
