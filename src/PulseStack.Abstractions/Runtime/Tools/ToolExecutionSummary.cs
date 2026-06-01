namespace PulseStack.Abstractions.Runtime.Tools;

public sealed class ToolExecutionSummary
{
    public int TotalInvocations { get; init; }

    public int SuccessfulExecutions { get; init; }

    public int FailedExecutions { get; init; }

    public TimeSpan TotalDuration { get; init; }

    public IReadOnlyList<ToolExecutionRecord> Executions { get; init; }
        = [];
}
