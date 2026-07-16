namespace PulseStack.Abstractions.Workflows;

public readonly record struct WorkflowStepId(Guid Value)
{
    public static WorkflowStepId New()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString();
}
