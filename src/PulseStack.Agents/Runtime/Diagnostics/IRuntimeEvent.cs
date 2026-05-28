namespace PulseStack.Agents.Runtime.Diagnostics;

public interface IRuntimeEvent
{
    Guid ExecutionId { get; }

    DateTimeOffset Timestamp { get; }

    IReadOnlyDictionary<string, object?> Metadata { get; }
}
