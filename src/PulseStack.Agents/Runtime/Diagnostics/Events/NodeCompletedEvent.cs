
namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record StepCompletedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string StepName,
    string StepType,
    bool Success)
    : IRuntimeEvent
{
    public IReadOnlyDictionary<string, object?> Metadata
        => new Dictionary<string, object?>
        {
            ["StepName"] = StepName,
            ["StepType"] = StepType,
            ["Success"] = Success
        };
}
