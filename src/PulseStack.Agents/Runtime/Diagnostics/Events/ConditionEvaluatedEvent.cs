using System;
using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Diagnostics.Events;

/// <summary>
/// Represents an event that is dispatched when a condition is evaluated in a pipeline.
/// </summary>
public sealed record ConditionEvaluatedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string StepName,
    bool Result) : IRuntimeEvent
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Metadata { get; } =
        new Dictionary<string, object?>
        {
            { "StepName", StepName },
            { "Result", Result }
        };
}