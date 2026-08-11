using PulseStack.Abstractions.Agents;

namespace PulseStack.Agents.Runtime;

internal static class AgentRuntimeMetadata
{
    public static string? ResolveModel(IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        return agent is Agent runtimeAgent
            ? runtimeAgent.Model
            : null;
    }
}