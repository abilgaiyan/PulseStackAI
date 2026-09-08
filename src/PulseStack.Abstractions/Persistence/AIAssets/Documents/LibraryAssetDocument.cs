using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

/// <summary>
/// Canonical portable representation of a declarative Library Asset.
/// </summary>
public sealed record LibraryAssetDocument : AIAssetDocument
{
    private readonly StructuralReadOnlyList<AIAssetReferenceDocument> members;

    public LibraryAssetDocument(
        AIAssetSchemaVersion schemaVersion,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        IEnumerable<AIAssetReferenceDocument>? members = null,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
        : base(
            schemaVersion,
            AIAssetDocumentType.Library,
            identity,
            metadata,
            lifecycle,
            references,
            dependencies)
    {
        this.members = new StructuralReadOnlyList<AIAssetReferenceDocument>(members);
    }

    /// <summary>
    /// Ordered semantic membership owned by the Library.
    /// </summary>
    public IReadOnlyList<AIAssetReferenceDocument> Members => members;
}
