namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

/// <summary>
/// Closed v1 discriminator for declarative Workflow step documents.
/// </summary>
public enum WorkflowStepDocumentKind
{
    Run,
    Parallel,
    Conditional,
    Retry,
    Loop,
    Switch
}
