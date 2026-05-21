using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Diagnostics.Events;

internal sealed record PipelineStartedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string PipelineName,
    int AgentCount,
    IReadOnlyDictionary<string, object?> Metadata)
    : IRuntimeEvent;
