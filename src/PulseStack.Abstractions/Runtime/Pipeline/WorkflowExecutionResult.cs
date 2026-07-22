using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Abstractions.Runtime.Pipeline;
public sealed class WorkflowExecutionResult 
{
    public bool Success { get; init; }

    public string FinalOutput { get; init; } = string.Empty;

    public IReadOnlyList<StepExecutionResult> Steps { get; init; } = [];
}