namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

/// <summary>
/// Base persistence contract for one declarative Workflow value definition.
/// </summary>
public abstract record WorkflowValueDocument
{
    protected WorkflowValueDocument(WorkflowValueDocumentKind kind)
    {
        Kind = kind;
    }

    public WorkflowValueDocumentKind Kind { get; }
}
