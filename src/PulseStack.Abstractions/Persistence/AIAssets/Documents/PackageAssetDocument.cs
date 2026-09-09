using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

/// <summary>
/// Canonical portable representation of a declarative Package Asset.
/// </summary>
public sealed record PackageAssetDocument : AIAssetDocument
{
    private readonly StructuralReadOnlyList<AIAssetReferenceDocument> members;

    public PackageAssetDocument(
        AIAssetSchemaVersion schemaVersion,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        IEnumerable<AIAssetReferenceDocument>? members = null,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
        : base(
            schemaVersion,
            AIAssetDocumentType.Package,
            identity,
            metadata,
            lifecycle,
            references,
            dependencies)
    {
        this.members = new StructuralReadOnlyList<AIAssetReferenceDocument>(members);
    }

    /// <summary>
    /// Ordered direct membership declared by the Package distribution boundary.
    /// </summary>
    public IReadOnlyList<AIAssetReferenceDocument> Members => members;
}
