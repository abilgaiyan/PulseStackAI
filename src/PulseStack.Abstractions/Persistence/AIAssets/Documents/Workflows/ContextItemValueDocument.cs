namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record ContextItemValueDocument : WorkflowValueDocument
{
    public ContextItemValueDocument(string key)
        : base(WorkflowValueDocumentKind.ContextItem)
    {
        Key = key;
    }

    public string Key { get; }
}
