namespace PulseStack.Abstractions.Assets;

public sealed record AssetReference(
    AssetType Type,
    AssetId Id,
    AssetUrn Urn,
    AssetVersion Version);
