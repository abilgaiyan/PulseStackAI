namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record StepstartedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string StepName,
    string StepType)
    : IRuntimeEvent
{
    public IReadOnlyDictionary<string, object?> Metadata
        => new Dictionary<string, object?>
        {
            ["StepName"] = StepName,
            ["StepType"] = StepType
        };
}
