using PulseStack.Abstractions.Agents;

namespace PulseStack.Agents.Runtime;

internal sealed record PipelineExecutionResult(
    bool Success,
    Guid ExecutionId,
    string FinalOutput,
    IReadOnlyList<PipelineStepResult> Steps,
    IReadOnlyList<string> Errors,
    TimeSpan Duration,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);
