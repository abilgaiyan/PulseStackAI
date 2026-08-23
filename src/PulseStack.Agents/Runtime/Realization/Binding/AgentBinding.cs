using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Agents.Realization.Binding;

public sealed record AgentBinding
{
    public required IToolExecutor ToolExecutor { get; init; }

    public IToolRegistry? Tools { get; init; }

    public IConversationMemory? Memory { get; init; }

    public float? Temperature { get; init; }
}