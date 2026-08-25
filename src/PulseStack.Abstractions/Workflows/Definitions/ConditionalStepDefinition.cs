using PulseStack.Abstractions.Workflows.Conditions;

namespace PulseStack.Abstractions.Workflows.Definitions;

/// <summary>
/// Declarative Workflow-language If step.
/// </summary>
public sealed record ConditionalStepDefinition : WorkflowStepDefinition
{
    public required string Name { get; init; }

    public required ConditionDefinition Condition { get; init; }

    public required WorkflowStepDefinition ThenStep { get; init; }

    public WorkflowStepDefinition? ElseStep { get; init; }
}
