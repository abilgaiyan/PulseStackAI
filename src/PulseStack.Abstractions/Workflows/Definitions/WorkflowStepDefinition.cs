namespace PulseStack.Abstractions.Workflows.Definitions;

/// <summary>
/// Declarative workflow step independent of runtime execution objects.
/// </summary>
public abstract record WorkflowStepDefinition
{
    public WorkflowStepId Id { get; init; } = WorkflowStepId.New();
}
