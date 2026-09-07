namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

/// <summary>
/// Base persistence contract for one declarative Workflow condition.
/// </summary>
public abstract record WorkflowConditionDocument
{
    protected WorkflowConditionDocument(WorkflowConditionDocumentKind kind)
    {
        Kind = kind;
    }

    public WorkflowConditionDocumentKind Kind { get; }
}
