namespace PulseStack.Abstractions.Workflows;

public readonly record struct WorkflowId(Guid Value)
{
    public static WorkflowId New()
        => new(Guid.NewGuid());

    public static WorkflowId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public override string ToString()
        => Value.ToString();

    public static implicit operator Guid(WorkflowId id)
        => id.Value;

    public static explicit operator WorkflowId(Guid value)
        => new(value);

    public void EnsureValid()
    {
        if (IsEmpty)
        {
            throw new ArgumentException(
                "WorkflowId cannot be empty.");
        }
    }
}
