using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime;

namespace PulseStack.Agents.Builders;

public sealed class AgentBuilder
{
    private readonly string _name;
    private readonly IChatClient _client;

    private string? _instructions;
    private float? _temperature;

    public AgentBuilder(string name, IChatClient client)
    {
        _name = name;
        _client = client;
    }

    public AgentBuilder WithInstructions(string text)
    {
        _instructions = text;
        return this;
    }

    public AgentBuilder WithTemperature(float value)
    {
        _temperature = value;
        return this;
    }

    public IAgent Build()
        => new Agent(_name, _client, _instructions, _temperature);
}