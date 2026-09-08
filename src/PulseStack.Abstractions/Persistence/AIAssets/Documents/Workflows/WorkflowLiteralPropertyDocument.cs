namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record WorkflowLiteralPropertyDocument
{
    public WorkflowLiteralPropertyDocument(
        string name,
        WorkflowLiteralDocument value)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }

    public WorkflowLiteralDocument Value { get; }
}
