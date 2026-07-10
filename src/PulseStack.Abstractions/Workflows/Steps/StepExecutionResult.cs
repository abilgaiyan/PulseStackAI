using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Abstractions.Workflows.Steps;
public sealed class StepExecutionResult
{
    public string StepName { get; init; } = string.Empty;

    public bool Success { get; init; }

    public string? Output { get; init; }

    public AIUsage? Usage { get; init; }
}