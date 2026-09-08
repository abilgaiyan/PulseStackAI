namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record NullWorkflowLiteralDocument : WorkflowLiteralDocument
{
    public NullWorkflowLiteralDocument()
        : base(WorkflowLiteralDocumentKind.Null)
    {
    }
}
