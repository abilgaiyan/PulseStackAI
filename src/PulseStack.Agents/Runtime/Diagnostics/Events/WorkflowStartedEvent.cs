
namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record WorkflowStartedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string WorkflowName,
    int StepCount)
    : IRuntimeEvent
{
    public IReadOnlyDictionary<string, object?> Metadata
        => new Dictionary<string, object?>
        {
            ["WorkflowName"] = WorkflowName,
            ["StepCount"] = StepCount
        };
}

