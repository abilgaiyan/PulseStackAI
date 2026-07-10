namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record NodeStartedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string StepName,
    string NodeType)
    : IRuntimeEvent
{
    public IReadOnlyDictionary<string, object?> Metadata
        => new Dictionary<string, object?>
        {
            ["StepName"] = StepName,
            ["NodeType"] = NodeType
        };
}
