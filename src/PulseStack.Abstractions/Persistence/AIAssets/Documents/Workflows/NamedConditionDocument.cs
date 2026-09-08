namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record NamedConditionDocument : WorkflowConditionDocument
{
    public NamedConditionDocument(string name)
        : base(WorkflowConditionDocumentKind.Named)
    {
        Name = name;
    }

    public string Name { get; }
}
