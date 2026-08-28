using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public abstract record AIAssetDocument
{
    private readonly StructuralReadOnlyList<AIAssetReferenceDocument> references;
    private readonly StructuralReadOnlyList<AIAssetDependencyDocument> dependencies;

    protected AIAssetDocument(
        AIAssetSchemaVersion schemaVersion,
        AIAssetDocumentType assetType,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
    {
        SchemaVersion = schemaVersion;
        AssetType = assetType;
        Identity = identity;
        Metadata = metadata;
        Lifecycle = lifecycle;
        this.references = new StructuralReadOnlyList<AIAssetReferenceDocument>(references);
        this.dependencies = new StructuralReadOnlyList<AIAssetDependencyDocument>(dependencies);
    }

    public AIAssetSchemaVersion SchemaVersion { get; }

    public AIAssetDocumentType AssetType { get; }

    public AIAssetIdentityDocument Identity { get; }

    public AIAssetMetadataDocument Metadata { get; }

    public AIAssetLifecycleDocument Lifecycle { get; }

    public IReadOnlyList<AIAssetReferenceDocument> References => references;

    public IReadOnlyList<AIAssetDependencyDocument> Dependencies => dependencies;
}
