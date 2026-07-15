namespace PulseStack.Abstractions.Common.Identity;

public readonly record struct WorkflowId(Guid Value)
{
    public static WorkflowId New()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString();
}
