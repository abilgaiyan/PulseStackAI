namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record BooleanWorkflowLiteralDocument : WorkflowLiteralDocument
{
    public BooleanWorkflowLiteralDocument(bool value)
        : base(WorkflowLiteralDocumentKind.Boolean)
    {
        Value = value;
    }

    public bool Value { get; }
}
