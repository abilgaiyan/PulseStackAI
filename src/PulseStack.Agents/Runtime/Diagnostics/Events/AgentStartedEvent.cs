using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record AgentStartedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string? AgentName,
    string? Model,
    Guid? BranchId,
    IReadOnlyDictionary<string, object?> Metadata)
    : IRuntimeEvent;
