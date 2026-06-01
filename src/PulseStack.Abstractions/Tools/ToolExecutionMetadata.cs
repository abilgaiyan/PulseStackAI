namespace PulseStack.Abstractions.Tools;

public sealed class ToolExecutionMetadata
{
    private readonly TimeSpan? _duration;

    public Guid ExecutionId { get; init; }
        = Guid.NewGuid();

    public DateTimeOffset StartedAt { get; init; }
        = DateTimeOffset.UtcNow;

    public DateTimeOffset CompletedAt { get; init; }
        = DateTimeOffset.UtcNow;

    public TimeSpan Duration
    {
        get => _duration ?? CompletedAt - StartedAt;
        init => _duration = value;
    }

    public bool Success { get; init; }

    public string ToolName { get; init; }
        = string.Empty;

    public string Category { get; init; }
        = string.Empty;
}
