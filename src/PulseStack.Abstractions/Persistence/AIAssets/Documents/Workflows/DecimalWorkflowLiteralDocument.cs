namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record DecimalWorkflowLiteralDocument : WorkflowLiteralDocument
{
    public DecimalWorkflowLiteralDocument(decimal value)
        : base(WorkflowLiteralDocumentKind.Decimal)
    {
        Value = value;
    }

    public decimal Value { get; }
}
