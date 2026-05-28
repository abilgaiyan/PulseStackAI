namespace PulseStack.Abstractions.Agents;

public sealed record PipelineStepResult(
    string AgentName,
    string? Model,
    string? Input,
    string? Output,
    bool Success,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt)
{
    public TimeSpan Duration =>
        CompletedAt - StartedAt;
}