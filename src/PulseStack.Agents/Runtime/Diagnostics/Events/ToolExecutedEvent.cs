using System;
using System.Collections.Generic;
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
    string Category = "",
    TimeSpan? Duration = null,
    IReadOnlyDictionary<string, object?> Metadata = null!)
    : IRuntimeEvent;
