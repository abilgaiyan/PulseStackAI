namespace PulseStack.Abstractions.Runtime.Tools;

public sealed class ToolExecutionRecord
{
    public string ToolName { get; init; }
        = string.Empty;

    public string Category { get; init; }
        = string.Empty;

    public bool Success { get; init; }

    public TimeSpan Duration { get; init; }
}
