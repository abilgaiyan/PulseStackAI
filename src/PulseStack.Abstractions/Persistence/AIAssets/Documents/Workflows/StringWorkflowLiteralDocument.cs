namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record StringWorkflowLiteralDocument : WorkflowLiteralDocument
{
    public StringWorkflowLiteralDocument(string value)
        : base(WorkflowLiteralDocumentKind.String)
    {
        Value = value;
    }

    public string Value { get; }
}
