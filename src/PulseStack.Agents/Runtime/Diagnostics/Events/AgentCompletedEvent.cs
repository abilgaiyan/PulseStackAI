using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Diagnostics.Events;

internal sealed record AgentCompletedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string? AgentName,
    string? Model,
    Guid? BranchId,
    bool IsSuccess,
    string? ErrorMessage,
    IReadOnlyDictionary<string, object?> Metadata)
    : IRuntimeEvent;
