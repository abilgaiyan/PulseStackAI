using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Agents.Runtime;

internal sealed class Agent : 
    IAgent, 
    IRuntimeAgent
{
    private readonly AgentRuntime _runtime;
    private readonly IReadOnlyCollection<string> _fallbackModels;
    private readonly string? _model;

    public string Name { get; }

    internal string? Model => _model;

    internal IReadOnlyCollection<string> FallbackModels =>
        _fallbackModels;

    public Agent(
        string name,
        IChatClient? client,
        IToolExecutor toolExecutor,
        string? instructions,
        float? temperature,
        IToolRegistry? tools,
        IConversationMemory? memory = null,
        string? model = null,
        IReadOnlyCollection<string>? fallbackModels = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(client);

        Name = name;
        _model = model;
        _fallbackModels = fallbackModels ?? [];

        _runtime = new AgentRuntime(
            client,
            toolExecutor,
            instructions,
            temperature,
            tools,
            memory,
            model,
            this);
    }

    public Agent(
        string name,
        IChatClientFactory? clientFactory,
        IToolExecutor toolExecutor,
        string model,
        string? instructions,
        float? temperature,
        IToolRegistry? tools,
        IConversationMemory? memory = null,
        IReadOnlyCollection<string>? fallbackModels = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        Name = name;
        _model = model;
        _fallbackModels = fallbackModels ?? [];

        _runtime = new AgentRuntime(
            clientFactory,
            toolExecutor,
            model,
            instructions,
            temperature,
            tools,
            memory,
            this);
    }

    public  Task<AgentResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var context = new PipelineContext
        {
            Input = input,
            CurrentOutput = input
        };

       return _runtime.RunAsync(
            context,
            cancellationToken);
    }

    async Task<AgentResponse> IRuntimeAgent.RunAsync(
        PipelineContext context,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(executionContext);

        return await _runtime.RunCoreAsync(
            context,
            executionContext,
            cancellationToken);
    }

    public IAsyncEnumerable<string> StreamAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        return _runtime.StreamAsync(
            input,
            cancellationToken);
    }
}