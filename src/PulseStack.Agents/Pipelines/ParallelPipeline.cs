using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Observability;

namespace PulseStack.Agents.Pipelines;

/// <summary>
/// Executes agents concurrently using the
/// centralized orchestration runtime.
/// </summary>
public sealed class ParallelPipeline
    : IAgentPipeline
{
    private readonly List<IAgent> _agents = [];

    private readonly PipelineRuntime _runtime;

    private readonly ParallelPipelineExecutionStrategy _strategy;

    public string Name { get; }

    public ParallelPipeline(string name)
        : this(
            name,
            new RuntimeEventDispatcher())
    {
    }

    public ParallelPipeline(
        string name,
        IRuntimeObserver observer)
        : this(
            name,
            new RuntimeEventDispatcher(observer))
    {
    }

    internal ParallelPipeline(
        string name,
        IRuntimeEventDispatcher eventDispatcher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;

        _runtime =
            new PipelineRuntime(eventDispatcher);

        _strategy =
            new ParallelPipelineExecutionStrategy();
    }

    public ParallelPipeline Add(
        IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _agents.Add(agent);

        return this;
    }

    public Task<PipelineResult> RunAsync(
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

    public async Task<PipelineResult> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var result =
            await _runtime.ExecuteAsync(
                Name,
                _agents,
                context,
                _strategy,
                cancellationToken: cancellationToken);

        return new PipelineResult(
            result.FinalOutput,
            result.Steps.ToList());
    }
}
