
namespace PulseStack.Abstractions.Assets;

public interface IAsset
{
    AssetId Id { get; }

    AssetUrn Urn { get; }

    AssetVersion Version { get; }

    AssetMetadata Metadata { get; }

    AssetType Type { get; }
}