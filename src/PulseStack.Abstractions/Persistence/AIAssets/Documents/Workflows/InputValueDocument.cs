namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record InputValueDocument : WorkflowValueDocument
{
    public InputValueDocument()
        : base(WorkflowValueDocumentKind.Input)
    {
    }
}
