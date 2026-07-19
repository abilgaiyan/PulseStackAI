using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;
namespace PulseStack.Tests.Fakes;

public sealed class FakeAgentResolver : IAgentResolver
{
    private readonly Dictionary<string, IAgent> _agents = new();

    public FakeAgentResolver()
    {
        Register(new FakeAgent("agent-alpha", "Agent Alpha"));
        Register(new FakeAgent("agent-beta", "Agent Beta"));
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