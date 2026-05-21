namespace PulseStack.Agents.Runtime.Diagnostics;

internal interface IRuntimeEvent
{
    Guid ExecutionId { get; }

    DateTimeOffset Timestamp { get; }

    IReadOnlyDictionary<string, object?> Metadata { get; }
}
