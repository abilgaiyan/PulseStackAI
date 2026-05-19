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
    private readonly IToolExecutor _toolExecutor;
    private readonly IChatClientFactory? _factory;
    private string? _model;
    private string? _instructions;
    private float? _temperature;
    private IToolRegistry? _tools;
    private IConversationMemory? _memory;
    private readonly List<string> _fallbackModels = [];

    public AgentBuilder(
        string name,
        IChatClient client,
        IToolExecutor toolExecutor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(toolExecutor);

        _name = name;
        _toolExecutor = toolExecutor;
        _client = client;
    }

    public AgentBuilder(
        string name,
        IChatClientFactory factory,
        IToolExecutor toolExecutor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(toolExecutor);
        ArgumentNullException.ThrowIfNull(factory);

        _name = name;
        _toolExecutor = toolExecutor;
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
        ArgumentNullException.ThrowIfNull(models);

        _fallbackModels.AddRange(
            models.Where(model =>
                !string.IsNullOrWhiteSpace(model)));

        return this;
    }
    
    public IAgent Build()
    {
        if (_client is not null)
        {
            return new Agent(
                _name,
                _client,
                _toolExecutor,
                _instructions,
                _temperature,
                _tools,
                _memory,
                _model,
                _fallbackModels);
        }

        if (_factory is not null)
        {
            if (string.IsNullOrWhiteSpace(_model))
            {
                throw new InvalidOperationException(
                    "No model configured.");
            }

            return new Agent(
                _name,
                _factory,
                _toolExecutor,
                _model,
                _instructions,
                _temperature,
                _tools,
                _memory,
                _fallbackModels);
        }

        throw new InvalidOperationException(
            "No chat client or factory configured.");
    }
}