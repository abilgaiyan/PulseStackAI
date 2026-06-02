namespace PulseStack.Agents.Runtime.Observability;

public static class PulseStackMetrics
{
    public const string PipelineExecutions =
        "pulse.pipeline.executions";

    public const string PipelineCompleted =
        "pulse.pipeline.completed";

    public const string PipelineFailed =
        "pulse.pipeline.failed";

    public const string PipelinePartialSuccess =
        "pulse.pipeline.partial_success";

    public const string PipelineCancelled =
        "pulse.pipeline.cancelled";

    public const string PipelineTimedOut =
        "pulse.pipeline.timed_out";

    public const string PipelineDuration =
        "pulse.pipeline.duration.ms";

    public const string AgentExecutions =
        "pulse.agent.executions";

    public const string AgentCompleted =
        "pulse.agent.completed";

    public const string AgentFailed =
        "pulse.agent.failed";

    public const string AgentRetries =
        "pulse.agent.retries";

    public const string AgentDuration =
        "pulse.agent.duration.ms";

    public const string ToolExecutions =
        "pulse.tool.executions";

    public const string ToolCompleted =
        "pulse.tool.completed";

    public const string ToolFailed =
        "pulse.tool.failed";

    public const string ToolDuration =
        "pulse.tool.duration.ms";

    public const string PromptTokens =
        "pulse.tokens.prompt";

    public const string CompletionTokens =
        "pulse.tokens.completion";

    public const string TotalTokens =
        "pulse.tokens.total";
}