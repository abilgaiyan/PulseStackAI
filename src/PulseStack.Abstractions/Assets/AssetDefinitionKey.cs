namespace PulseStack.Abstractions.Assets;

/// <summary>
/// Identifies one immutable Asset definition.
/// </summary>
public readonly record struct AssetDefinitionKey(
    AssetType Type,
    AssetId Id,
    AssetVersion Version)
{
    public static AssetDefinitionKey From(IAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return new AssetDefinitionKey(asset.Type, asset.Id, asset.Version);
    }

    public static AssetDefinitionKey From(AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(reference);
        return new AssetDefinitionKey(reference.Type, reference.Id, reference.Version);
    }
}
