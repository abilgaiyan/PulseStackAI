using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record ToolExecutedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string ToolName,
    string Input,
    string? AgentName,
    Guid? BranchId,
    bool IsSuccess,
    string? ErrorMessage,
    IReadOnlyDictionary<string, object?> Metadata,
    string Category = "",
    TimeSpan? Duration = null)
    : IRuntimeEvent;
