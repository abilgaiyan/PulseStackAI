using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Runtime.Diagnostics.Events;

public sealed class AgentRetryEvent : IRuntimeEvent
{
    public Guid ExecutionId { get; }

    public DateTimeOffset Timestamp { get; }

    public string AgentName { get; }

    public int Attempt { get; }

    public string Error { get; }

    public IReadOnlyDictionary<string, object?> Metadata => new Dictionary<string, object?>()
    {
        { "AgentName", AgentName },
        { "Attempt", Attempt },
        { "Error", Error }
    };

    public AgentRetryEvent(
        Guid executionId,
        DateTimeOffset timestamp,
        string agentName,
        int attempt,
        string error)
    {
        ExecutionId = executionId;
        Timestamp = timestamp;
        AgentName = agentName;
        Attempt = attempt;
        Error = error;
    }
}