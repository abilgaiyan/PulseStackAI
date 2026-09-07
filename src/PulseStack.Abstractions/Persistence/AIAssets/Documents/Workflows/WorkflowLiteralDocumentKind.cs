namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

/// <summary>
/// Closed v1 discriminator for canonical Workflow literal documents.
/// </summary>
public enum WorkflowLiteralDocumentKind
{
    Null,
    String,
    Boolean,
    Integer,
    Decimal,
    Array,
    Object
}
