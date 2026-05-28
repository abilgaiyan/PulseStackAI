using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Observability;

namespace PulseStack.Agents.Pipelines;

/// <summary>
/// Executes agents sequentially,
/// passing each agent output
/// to the next agent.
/// </summary>
public sealed class SequentialPipeline
    : IAgentPipeline
{
    private readonly List<IAgent> _agents = [];

    private readonly PipelineRuntime _runtime;

    private readonly IPipelineExecutionStrategy _strategy;
    public string Name { get; }
    private PipelineExecutionPolicy _policy = new();

    public SequentialPipeline(string name) 
        : this(
            name,
            new PipelineRuntime(),
            new SequentialPipelineExecutionStrategy())
    {
    }

    public SequentialPipeline(
        string name,
        IRuntimeObserver observer)
        : this(
            name,
            new PipelineRuntime(
                new RuntimeEventDispatcher(observer)),
            new SequentialPipelineExecutionStrategy())
    {
    }

    internal SequentialPipeline(
        string name,
        PipelineRuntime runtime,
        IPipelineExecutionStrategy strategy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;

        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));

        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));

    }

    public SequentialPipeline Add(
        IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _agents.Add(agent);

        return this;
    }
    public SequentialPipeline WithPolicy(
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

    public Task<PipelineExecutionResult> RunDetailedAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        var context = new PipelineContext
        {
            Input = input,
            CurrentOutput = input
        };

        return RunDetailedAsync(
            context,
            cancellationToken);
    }

    public Task<PipelineExecutionResult> RunDetailedAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result =
            _runtime.ExecuteAsync(
                Name,
                _agents,
                context,
                _strategy,
                _policy,
                cancellationToken: cancellationToken);

        return result;
    }
}
