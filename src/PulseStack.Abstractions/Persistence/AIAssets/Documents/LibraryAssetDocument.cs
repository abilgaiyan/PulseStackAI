using PulseStack.Abstractions.AIAssets;
using PulseStack.Abstractions.AIAssets.References;
using PulseStack.Abstractions.Persistence.AIAssets.Internal;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public sealed record LibraryAssetDocument : AIAssetDocument
{
    private readonly StructuralReadOnlyList<AIAssetReferenceDocument> members;

    public LibraryAssetDocument(
        AIAssetSchemaVersion schemaVersion,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        IEnumerable<AIAssetReferenceDocument>? members,
        IEnumerable<AIAssetReferenceDocument>? references,
        IEnumerable<AIAssetDependencyDocument>? dependencies)
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

    public IReadOnlyList<AIAssetReferenceDocument> Members
        => members;
}
