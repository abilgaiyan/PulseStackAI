namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record ArrayWorkflowLiteralDocument : WorkflowLiteralDocument
{
    private readonly StructuralReadOnlyList<WorkflowLiteralDocument> items;

    public ArrayWorkflowLiteralDocument(
        IEnumerable<WorkflowLiteralDocument>? items = null)
        : base(WorkflowLiteralDocumentKind.Array)
    {
        this.items = new StructuralReadOnlyList<WorkflowLiteralDocument>(items);
    }

    public IReadOnlyList<WorkflowLiteralDocument> Items => items;
}
