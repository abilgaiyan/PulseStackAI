namespace PulseStack.Abstractions.Workflows;

public readonly record struct WorkflowId(Guid Value)
{
    public static WorkflowId New()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString();

     public static WorkflowId Empty => new(Guid.Empty);    
}
