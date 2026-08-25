namespace PulseStack.Abstractions.Workflows.Definitions;

/// <summary>
/// Declarative parallel workflow step.
/// </summary>
public sealed record ParallelStepDefinition : WorkflowStepDefinition
{
    public required string Name { get; init; }

    public IReadOnlyList<WorkflowStepDefinition> Steps { get; init; } = [];
}
