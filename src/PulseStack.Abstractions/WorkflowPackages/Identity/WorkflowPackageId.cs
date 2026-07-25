namespace PulseStack.Abstractions.WorkflowPackages.Identity;

public readonly record struct WorkflowPackageId(Guid Value)
{
    public static WorkflowPackageId New() => new(Guid.NewGuid());

    public static WorkflowPackageId Empty => new(Guid.Empty);

    public bool IsEmpty => Value == Guid.Empty;

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(WorkflowPackageId id) => id.Value;

    public static explicit operator WorkflowPackageId(Guid value) => new(value);
}
