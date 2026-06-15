using System.Threading;
using System.Threading.Tasks;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Agents.Routing;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Observability;
using PulseStack.Agents.Runtime.Diagnostics.Events;

namespace PulseStack.Agents.Pipelines;

public sealed class RouterPipeline
    : IAgentPipeline
{
    private readonly List<IAgent> _agents = [];

    private readonly IAgentSelector _selector;

    private readonly PipelineRuntime _runtime;

    private readonly IPipelineExecutionStrategy _strategy;

    private readonly IRuntimeEventDispatcher _eventDispatcher;

    private PipelineExecutionPolicy _policy = new();

    public string Name { get; }

    public RouterPipeline(
        string name,
        IAgentSelector selector,
        IRuntimeObserver observer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(observer);

        Name = name;

        _selector = selector;

        _eventDispatcher =
            new RuntimeEventDispatcher(observer);

        var agentRuntime =
            new AgentRuntime(_eventDispatcher);

        _runtime =
            new PipelineRuntime(_eventDispatcher);

        _strategy =
            new SequentialPipelineExecutionStrategy(
                agentRuntime);
    }

    public RouterPipeline Add(
        IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _agents.Add(agent);

        return this;
    }

    public RouterPipeline WithPolicy(
        PipelineExecutionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        _policy = policy;

        return this;
    }

    public async Task<PipelineResult> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var detailed =
            await RunDetailedAsync(
                input,
                cancellationToken);

        return new PipelineResult(
            detailed.FinalOutput,
            detailed.Steps);
    }

    public async Task<PipelineResult> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var detailed =
            await RunDetailedAsync(
                context,
                cancellationToken);

        return new PipelineResult(
            detailed.FinalOutput,
            detailed.Steps);
    }

    public async Task<PipelineExecutionResult> RunDetailedAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var context =
            new PipelineContext
            {
                Input = input,
                CurrentOutput = input
            };

        return await RunDetailedAsync(
            context,
            cancellationToken);
    }
    public async Task<PipelineExecutionResult> RunDetailedAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_agents.Count == 0)
        {
            throw new InvalidOperationException(
                "Router contains no agents.");
        }

        var selected =
            await _selector.SelectAsync(
                context,
                _agents,
                cancellationToken);

        context.Items["SelectedAgent"] = selected.Name;                

        _eventDispatcher.Dispatch(
            new AgentSelectedEvent(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                _selector.GetType().Name,
                selected.Name,
                null));

        // TODO:
        // evolve IAgentSelector to return
        // AgentSelectionResult so routing
        // reason can be captured.                

        return await _runtime.ExecuteAsync(
            Name,
            [selected],
            context,
            _strategy,
            _policy,
            cancellationToken);
    }
}
