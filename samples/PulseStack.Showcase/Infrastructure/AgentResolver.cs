using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Showcase.Infrastructure;
public sealed class AgentResolver : IAgentResolver
{
    private readonly Dictionary<string, IAgent> _agents = new();

    public AgentResolver()
    {
        Register(new SampleAgent("approval-agent", "Approval Agent"));
        Register(new SampleAgent("notification-agent", "Notification Agent"));
    }

    public void Register(IAgent agent)
    {
        _agents.Add(agent.Name, agent);
    }

    public IAgent Resolve(string name)
    {
        return _agents[name];
    }
}