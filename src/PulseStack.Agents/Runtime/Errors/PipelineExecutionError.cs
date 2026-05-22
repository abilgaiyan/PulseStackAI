namespace PulseStack.Agents.Runtime.Errors;

public sealed class PipelineExecutionError
{
    public string Code { get; init; } = "runtime_error";

    public string Message { get; init; } = string.Empty;

    public string? AgentName { get; init; }

    public Exception? Exception { get; init; }

    public DateTimeOffset Timestamp { get; init; }
        = DateTimeOffset.UtcNow;
}
