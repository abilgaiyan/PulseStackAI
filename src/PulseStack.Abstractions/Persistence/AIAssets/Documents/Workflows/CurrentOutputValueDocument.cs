namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record CurrentOutputValueDocument : WorkflowValueDocument
{
    public CurrentOutputValueDocument()
        : base(WorkflowValueDocumentKind.CurrentOutput)
    {
    }
}
