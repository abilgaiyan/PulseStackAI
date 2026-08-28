using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public sealed record AIAssetReferenceDocument
{
    public required AIAssetDocumentType AssetType { get; init; }

    public required string AssetId { get; init; }

    public required string Urn { get; init; }

    public required string Version { get; init; }
}
