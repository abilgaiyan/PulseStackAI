
namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record NodeCompletedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string StepName,
    string NodeType,
    bool Success)
    : IRuntimeEvent
{
    public IReadOnlyDictionary<string, object?> Metadata
        => new Dictionary<string, object?>
        {
            ["StepName"] = StepName,
            ["NodeType"] = NodeType,
            ["Success"] = Success
        };
}
