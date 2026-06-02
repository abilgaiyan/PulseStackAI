using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record AgentCompletedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string? AgentName,
    string? Model,
    Guid? BranchId,
    bool IsSuccess,
    string? ErrorMessage,
    TimeSpan Duration,
    IReadOnlyDictionary<string, object?> Metadata)
    : IRuntimeEvent;
