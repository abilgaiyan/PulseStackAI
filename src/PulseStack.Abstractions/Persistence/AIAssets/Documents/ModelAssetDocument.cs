using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public sealed record ModelAssetDocument : AIAssetDocument
{
    public ModelAssetDocument(
        AIAssetSchemaVersion schemaVersion,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        string provider,
        string model,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
        : base(
            schemaVersion,
            AIAssetDocumentType.Model,
            identity,
            metadata,
            lifecycle,
            references,
            dependencies)
    {
        Provider = provider;
        Model = model;
    }

    public string Provider { get; }

    public string Model { get; }
}
