namespace PulseStack.Abstractions.Workflows.Definitions;

/// <summary>
/// Declarative Switch branch in the Workflow Language.
/// </summary>
public sealed record SwitchCaseDefinition
{
    public required string Value { get; init; }

    public required WorkflowStepDefinition Step { get; init; }
}
