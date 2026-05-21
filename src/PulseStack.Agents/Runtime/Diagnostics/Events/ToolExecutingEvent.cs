using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Diagnostics.Events;

internal sealed record ToolExecutingEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string ToolName,
    string Input,
    string? AgentName,
    Guid? BranchId,
    IReadOnlyDictionary<string, object?> Metadata)
    : IRuntimeEvent;
