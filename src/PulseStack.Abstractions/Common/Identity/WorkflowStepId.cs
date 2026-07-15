namespace PulseStack.Abstractions.Common.Identity;

public readonly record struct WorkflowStepId(Guid Value)
{
    public static WorkflowStepId New()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString();
}
