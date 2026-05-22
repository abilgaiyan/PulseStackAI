namespace PulseStack.Agents.Runtime.Policies;

public sealed class PipelineExecutionPolicy
{
    /// <summary>
    /// Maximum execution duration allowed for the pipeline.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Number of retry attempts for pipeline execution.
    /// </summary>
    public int MaxRetries { get; init; } = 0;

    /// <summary>
    /// Continue pipeline execution if an agent fails.
    /// </summary>
    public bool ContinueOnAgentFailure { get; init; } = true;

    /// <summary>
    /// Maximum concurrent execution branches.
    /// Reserved for future runtime scheduling support.
    /// </summary>
    public int? MaxParallelism { get; init; }

    /// <summary>
    /// Enables runtime diagnostics capture.
    /// </summary>
    public bool CaptureDiagnostics { get; init; } = true;
}
