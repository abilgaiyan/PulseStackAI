namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record IntegerWorkflowLiteralDocument : WorkflowLiteralDocument
{
    public IntegerWorkflowLiteralDocument(long value)
        : base(WorkflowLiteralDocumentKind.Integer)
    {
        Value = value;
    }

    public long Value { get; }
}
