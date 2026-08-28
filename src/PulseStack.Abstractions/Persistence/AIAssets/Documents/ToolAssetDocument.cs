using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public sealed record ToolAssetDocument : AIAssetDocument
{
    public ToolAssetDocument(
        AIAssetSchemaVersion schemaVersion,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
        : base(
            schemaVersion,
            AIAssetDocumentType.Tool,
            identity,
            metadata,
            lifecycle,
            references,
            dependencies)
    {
    }
}
