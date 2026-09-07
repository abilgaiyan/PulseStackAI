namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

/// <summary>
/// Base persistence contract for one declarative Workflow step.
/// </summary>
public abstract record WorkflowStepDocument
{
    protected WorkflowStepDocument(
        WorkflowStepDocumentKind kind,
        string stepId)
    {
        Kind = kind;
        StepId = stepId;
    }

    public WorkflowStepDocumentKind Kind { get; }

    /// <summary>
    /// Canonical lowercase GUID "D" representation of WorkflowStepId.Value.
    /// Structural validation owns canonical-format, non-empty, and global uniqueness checks.
    /// </summary>
    public string StepId { get; }
}
