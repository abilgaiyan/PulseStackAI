using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Safe structured context for an MS-009.7 failure.
/// Serialized asset bytes and asset content are intentionally excluded.
/// </summary>
public sealed record AIAssetStorageDiagnosticContext
{
    public AIAssetStorageOperation? Operation { get; init; }

    public AssetDefinitionKey? Key { get; init; }

    public long? RepresentationSizeBytes { get; init; }

    public long? MaximumRepresentationSizeBytes { get; init; }
}
