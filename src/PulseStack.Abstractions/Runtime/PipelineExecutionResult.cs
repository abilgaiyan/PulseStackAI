using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Errors;
using PulseStack.Abstractions.Runtime.Execution;

namespace PulseStack.Abstractions.Runtime.Pipeline;

public sealed class PipelineExecutionResult
{
    public Guid ExecutionId { get; init; }

    public bool Success { get; init; }

    public ExecutionStatus Status { get; init; }

    public string FinalOutput { get; init; } = string.Empty;

    public IReadOnlyList<PipelineStepResult> Steps { get; init; }
        = [];

    public IReadOnlyList<PipelineExecutionError> Errors { get; init; }
        = [];

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset CompletedAt { get; init; }

    public TimeSpan Duration { get; init; }
}
