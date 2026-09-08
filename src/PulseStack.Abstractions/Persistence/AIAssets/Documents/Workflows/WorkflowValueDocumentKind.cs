namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

/// <summary>
/// Closed v1 discriminator for declarative Workflow value documents.
/// </summary>
public enum WorkflowValueDocumentKind
{
    Input,
    CurrentOutput,
    ContextItem,
    Literal
}
