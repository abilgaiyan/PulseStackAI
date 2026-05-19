namespace PulseStack.Abstractions.Tools;

public sealed class ToolExecutionMetadata
{
    public Guid ExecutionId { get; init; }
        = Guid.NewGuid();

    public string ToolName { get; init; }
        = string.Empty;

    public DateTimeOffset StartedAt { get; init; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset CompletedAt { get; init; }
        = DateTimeOffset.UtcNow;

    public TimeSpan Duration
        => CompletedAt - StartedAt;
}