using PulseStack.Abstractions.Runtime.Usage;

namespace PulseStack.Abstractions.Agents;

public sealed record AgentResponse
{
    public string Text { get; init; } = string.Empty;

     public string? Model { get; init; }

    public AIUsage? Usage { get; init; }
}