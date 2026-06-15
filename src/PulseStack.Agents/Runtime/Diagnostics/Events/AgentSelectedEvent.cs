using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed record AgentSelectedEvent(
    Guid ExecutionId,
    DateTimeOffset Timestamp,
    string SelectorName,
    string AgentName,
    string? Reason)
    : IRuntimeEvent
{
    public IReadOnlyDictionary<string, object?> Metadata
        => new Dictionary<string, object?>
        {
            ["Selector"] = SelectorName,
            ["Agent"] = AgentName,
            ["Reason"] = Reason
        };
}