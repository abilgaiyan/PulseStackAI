namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record SwitchCaseDocument
{
    public SwitchCaseDocument(
        string value,
        WorkflowStepDocument step)
    {
        Value = value;
        Step = step;
    }

    public string Value { get; }

    public WorkflowStepDocument Step { get; }
}
