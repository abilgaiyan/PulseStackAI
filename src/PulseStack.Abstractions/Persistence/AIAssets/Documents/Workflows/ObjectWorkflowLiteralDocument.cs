namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record ObjectWorkflowLiteralDocument : WorkflowLiteralDocument
{
    private readonly StructuralReadOnlyList<WorkflowLiteralPropertyDocument> properties;

    public ObjectWorkflowLiteralDocument(
        IEnumerable<WorkflowLiteralPropertyDocument>? properties = null)
        : base(WorkflowLiteralDocumentKind.Object)
    {
        this.properties = new StructuralReadOnlyList<WorkflowLiteralPropertyDocument>(properties);
    }

    public IReadOnlyList<WorkflowLiteralPropertyDocument> Properties => properties;
}
