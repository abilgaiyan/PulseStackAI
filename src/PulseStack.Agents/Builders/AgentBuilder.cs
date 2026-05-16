using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Tools;
using PulseStack.Abstractions.Chat;
using PulseStack.Agents.Runtime;

namespace PulseStack.Agents.Builders;

public sealed class AgentBuilder
{
    private readonly string _name;
    private readonly IChatClient? _client;
    private readonly IChatClientFactory? _factory;
    private string? _model;
    private string? _instructions;
    private float? _temperature;
    private IToolRegistry? _tools;
    private IConversationMemory? _memory;
    private readonly List<string> _fallbackModels = [];

    public AgentBuilder(
        string name,
        IChatClient client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(client);

        _name = name;
        _client = client;
    }

    public AgentBuilder(
        string name,
        IChatClientFactory factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);

        _name = name;
        _factory = factory;
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

    public AgentBuilder WithModel(string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        _model = model;

        return this;
    }

    public AgentBuilder WithFallbackModels(
        params string[] models)
    {
        _fallbackModels.AddRange(models);

        return this;
    }
    
    public IAgent Build()
    {
        IChatClient client;

        if (_client is not null)
        {
            client = _client;
        }
        else
        {
            if (_factory is null)
            {
                throw new InvalidOperationException(
                    "No chat client or factory configured.");
            }

            if (string.IsNullOrWhiteSpace(_model))
            {
                throw new InvalidOperationException(
                    "No model configured.");
            }

            client = _factory.Create(_model);
        }

        return new Agent(_name, client, _instructions, _temperature, _tools, _memory, _model, _fallbackModels);
    }
}