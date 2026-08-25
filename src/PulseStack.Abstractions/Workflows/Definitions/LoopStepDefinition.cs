using PulseStack.Abstractions.Workflows.Values;

namespace PulseStack.Abstractions.Workflows.Definitions;

/// <summary>
/// Declarative ForEach workflow step.
/// </summary>
public sealed record LoopStepDefinition : WorkflowStepDefinition
{
    public string Name { get; init; } = "ForEach";

    public required WorkflowValueDefinition Items { get; init; }

    public required WorkflowStepDefinition Step { get; init; }
}
