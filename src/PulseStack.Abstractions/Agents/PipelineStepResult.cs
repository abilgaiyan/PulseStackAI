namespace PulseStack.Abstractions.Agents;

public sealed record PipelineStepResult(
    string AgentName,
    string? Model,
    string? Input,
    string? Output,
    bool Success,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int? RetryCount = 0)
{
    public TimeSpan Duration =>
        CompletedAt - StartedAt;
}