namespace PulseStack.Abstractions.Workflows.Definitions;

/// <summary>
/// Declarative retry grammar for a child workflow step.
/// </summary>
public sealed record RetryStepDefinition : WorkflowStepDefinition
{
    public string Name { get; init; } = "Retry";

    public required WorkflowStepDefinition Step { get; init; }

    public int MaxAttempts { get; init; } = 3;
}
