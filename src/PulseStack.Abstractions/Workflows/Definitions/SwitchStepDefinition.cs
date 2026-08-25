using PulseStack.Abstractions.Workflows.Values;

namespace PulseStack.Abstractions.Workflows.Definitions;

/// <summary>
/// Declarative Switch grammar independent of runtime selector delegates.
/// </summary>
public sealed record SwitchStepDefinition : WorkflowStepDefinition
{
    public string Name { get; init; } = "Switch";

    public required WorkflowValueDefinition Selector { get; init; }

    public IReadOnlyList<SwitchCaseDefinition> Cases { get; init; } = [];

    public WorkflowStepDefinition? DefaultStep { get; init; }
}
