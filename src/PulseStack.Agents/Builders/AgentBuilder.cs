using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Tools;
using PulseStack.Agents.Runtime;

namespace PulseStack.Agents.Builders;

public sealed class AgentBuilder
{
    private readonly string _name;
    private readonly IChatClient _client;
    private string? _instructions;
    private float? _temperature;
    private IToolRegistry? _tools;
    private IConversationMemory? _memory;

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

    public AgentBuilder WithTools(IToolRegistry tools)
    {
        _tools = tools;
        return this;
    }
    
    public AgentBuilder WithMemory(IConversationMemory memory)
    {
        _memory = memory;
        return this;
    }

    public IAgent Build()
        => new Agent(_name, _client, _instructions, _temperature, _tools, _memory);
}