namespace PulseStack.Abstractions.Workflows.Definitions;

/// <summary>
/// Declarative workflow step independent of runtime execution objects.
/// </summary>
public abstract record WorkflowStepDefinition
{
    public required WorkflowStepId Id { get; init; }
}
