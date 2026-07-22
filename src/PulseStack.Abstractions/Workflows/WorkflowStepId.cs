namespace PulseStack.Abstractions.Workflows;

/// <summary>
/// Step identity of the workflow itself.
///
/// Because a workflow is also a workflow step,
/// this identifier is used when a workflow appears
/// as a composite node in larger workflow graphs.
/// </summary>
public readonly record struct WorkflowStepId(Guid Value)
{
    public static WorkflowStepId New()
        => new(Guid.NewGuid());

    public override string ToString()
        => Value.ToString();
}
