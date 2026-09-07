namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

/// <summary>
/// Base persistence contract for one canonical Workflow literal value.
/// </summary>
public abstract record WorkflowLiteralDocument
{
    private protected WorkflowLiteralDocument(WorkflowLiteralDocumentKind kind)
    {
        Kind = kind;
    }

    public WorkflowLiteralDocumentKind Kind { get; }
}
