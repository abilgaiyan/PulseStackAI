
namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record WorkflowStartedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string WorkflowName,
    int NodeCount)
    : IRuntimeEvent
{
    public IReadOnlyDictionary<string, object?> Metadata
        => new Dictionary<string, object?>
        {
            ["WorkflowName"] = WorkflowName,
            ["NodeCount"] = NodeCount
        };
}

