using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

/// <summary>
/// Canonical portable representation of a declarative Workflow Asset.
/// </summary>
public sealed record WorkflowAssetDocument : AIAssetDocument
{
    private readonly StructuralReadOnlyList<WorkflowStepDocument> steps;

    public WorkflowAssetDocument(
        AIAssetSchemaVersion schemaVersion,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        IEnumerable<WorkflowStepDocument>? steps = null,
        IEnumerable<AIAssetReferenceDocument>? references = null,
        IEnumerable<AIAssetDependencyDocument>? dependencies = null)
        : base(
            schemaVersion,
            AIAssetDocumentType.Workflow,
            identity,
            metadata,
            lifecycle,
            references,
            dependencies)
    {
        this.steps = new StructuralReadOnlyList<WorkflowStepDocument>(steps);
    }

    /// <summary>
    /// Ordered root declarative Workflow steps.
    /// </summary>
    public IReadOnlyList<WorkflowStepDocument> Steps => steps;
}
