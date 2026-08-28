using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public abstract record AIAssetDocument
{
    private readonly IReadOnlyList<AIAssetReferenceDocument> references;
    private readonly IReadOnlyList<AIAssetDependencyDocument> dependencies;

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
        this.references = Array.AsReadOnly(
            references?.ToArray() ?? Array.Empty<AIAssetReferenceDocument>());
        this.dependencies = Array.AsReadOnly(
            dependencies?.ToArray() ?? Array.Empty<AIAssetDependencyDocument>());
    }

    public AIAssetSchemaVersion SchemaVersion { get; }

    public AIAssetDocumentType AssetType { get; }

    public AIAssetIdentityDocument Identity { get; }

    public AIAssetMetadataDocument Metadata { get; }

    public AIAssetLifecycleDocument Lifecycle { get; }

    public IReadOnlyList<AIAssetReferenceDocument> References => references;

    public IReadOnlyList<AIAssetDependencyDocument> Dependencies => dependencies;
}
