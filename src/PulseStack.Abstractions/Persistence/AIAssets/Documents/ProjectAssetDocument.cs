using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

/// <summary>
/// Canonical portable representation of a declarative Project Asset.
/// </summary>
public sealed record ProjectAssetDocument : AIAssetDocument
{
    private readonly StructuralReadOnlyList<AIAssetReferenceDocument> ownedAssets;

    public ProjectAssetDocument(
        AIAssetSchemaVersion schemaVersion,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        AIAssetReferenceDocument? entryWorkflow,
        IEnumerable<AIAssetReferenceDocument>? ownedAssets = null,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
        : base(
            schemaVersion,
            AIAssetDocumentType.Project,
            identity,
            metadata,
            lifecycle,
            references,
            dependencies)
    {
        EntryWorkflow = entryWorkflow;
        this.ownedAssets = new StructuralReadOnlyList<AIAssetReferenceDocument>(ownedAssets);
    }

    /// <summary>
    /// Canonical entry Workflow reference. Nullable so malformed portable documents remain representable for validation.
    /// </summary>
    public AIAssetReferenceDocument? EntryWorkflow { get; }

    /// <summary>
    /// Ordered semantic membership owned by the Project.
    /// </summary>
    public IReadOnlyList<AIAssetReferenceDocument> OwnedAssets => ownedAssets;
}
