namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record LiteralValueDocument : WorkflowValueDocument
{
    public LiteralValueDocument(WorkflowLiteralDocument literal)
        : base(WorkflowValueDocumentKind.Literal)
    {
        Literal = literal;
    }

    public WorkflowLiteralDocument Literal { get; }
}
