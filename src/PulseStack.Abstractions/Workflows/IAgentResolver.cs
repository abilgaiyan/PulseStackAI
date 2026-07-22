using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Workflows;

public interface IAgentResolver
{
    IAgent Resolve(string agentReference);
}