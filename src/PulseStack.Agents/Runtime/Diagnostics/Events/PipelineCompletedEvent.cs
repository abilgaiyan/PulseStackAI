using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record PipelineCompletedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string PipelineName,
    int AgentCount,
    int SuccessfulAgentCount,
    int FailedAgentCount,
    TimeSpan Duration,
    AIUsage TotalUsage,
    IReadOnlyDictionary<string, object?> Metadata)
    : IRuntimeEvent;
    
