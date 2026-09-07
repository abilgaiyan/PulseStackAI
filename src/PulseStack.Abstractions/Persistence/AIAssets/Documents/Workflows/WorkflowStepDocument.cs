namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

/// <summary>
/// Base persistence contract for a declarative Workflow step.
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
    /// Portable representation of WorkflowStepId.
    /// Structural validation owns non-empty and global uniqueness rules.
    /// </summary>
    public string StepId { get; }
}
