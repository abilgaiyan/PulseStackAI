
namespace PulseStack.Abstractions.Runtime.Diagnostics;

public sealed class ConditionExecutionRecord
{
    public required string ConditionName { get; init; }

    public bool Result { get; init; }

    public TimeSpan Duration { get; init; }

    public DateTimeOffset Timestamp { get; init; }
}