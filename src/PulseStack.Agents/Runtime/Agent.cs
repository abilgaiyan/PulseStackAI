using Microsoft.Extensions.AI;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Tools;

namespace PulseStack.Agents.Runtime;

internal sealed class Agent : IAgent
{
    private readonly IAgentRuntime _runtime;
    private readonly IReadOnlyCollection<string>? _fallbackModels;
    private readonly string? _model;

    public string Name { get; }
    public string? Model => _model;
    public IReadOnlyCollection<string>? FallbackModels => _fallbackModels;

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
            model);
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
            memory);
    }

    public Task<ChatResponse> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var context = new PipelineContext
        {
            Input = input,
            CurrentOutput = input
        };

        return RunAsync(
            context,
            cancellationToken);
    }

    public Task<ChatResponse> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _runtime.RunAsync(
            context,
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
