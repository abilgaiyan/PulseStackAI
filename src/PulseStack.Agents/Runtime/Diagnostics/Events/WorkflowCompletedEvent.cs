
namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record WorkflowCompletedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string WorkflowName,
    bool Success)
    : IRuntimeEvent
{
    public IReadOnlyDictionary<string, object?> Metadata
        => new Dictionary<string, object?>
        {
            ["WorkflowName"] = WorkflowName,
            ["Success"] = Success
        };
}