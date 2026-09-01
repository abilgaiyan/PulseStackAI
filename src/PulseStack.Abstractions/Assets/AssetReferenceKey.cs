namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Proves exact equivalence of an external Asset reference.
/// </summary>
public readonly record struct AssetReferenceKey(
    AssetType Type,
    AssetId Id,
    AssetUrn Urn,
    AssetVersion Version)
{
    public static AssetReferenceKey From(AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new AssetReferenceKey(
            reference.Type,
            reference.Id,
            reference.Urn,
            reference.Version);
    }

    public static AssetReferenceKey From(IAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return new AssetReferenceKey(
            asset.Type,
            asset.Id,
            asset.Urn,
            asset.Version);
    }
}
