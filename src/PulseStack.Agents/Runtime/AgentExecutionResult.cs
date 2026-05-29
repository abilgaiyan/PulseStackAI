using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Agents.Runtime;

public sealed class AgentExecutionResult
{
    public bool Success { get; init; }

    public string Output { get; init; } = string.Empty;

    public int RetryCount { get; init; }

    public Exception? Exception { get; init; }

    public AIUsage? Usage { get; init; }

    public DateTimeOffset StartedAt { get; init; }

    public DateTimeOffset CompletedAt { get; init; }
}
