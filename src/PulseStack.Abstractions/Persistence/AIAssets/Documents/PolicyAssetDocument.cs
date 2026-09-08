using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public sealed record PolicyAssetDocument : AIAssetDocument
{
    public PolicyAssetDocument(
        AIAssetSchemaVersion schemaVersion,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
        : base(
            schemaVersion,
            AIAssetDocumentType.Policy,
            identity,
            metadata,
            lifecycle,
            references,
            dependencies)
    {
    }
}
