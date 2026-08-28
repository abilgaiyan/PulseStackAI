using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public sealed record PromptAssetDocument : AIAssetDocument
{
    public PromptAssetDocument(
        AIAssetSchemaVersion schemaVersion,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        string systemInstructions,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
        : base(
            schemaVersion,
            AIAssetDocumentType.Prompt,
            identity,
            metadata,
            lifecycle,
            references,
            dependencies)
    {
        SystemInstructions = systemInstructions;
    }

    public string SystemInstructions { get; }
}
