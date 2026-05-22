using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Diagnostics;

namespace PulseStack.Agents.Pipelines;

/// <summary>
/// Executes agents concurrently against isolated
/// branched execution contexts.
/// </summary>
public sealed class ParallelPipeline
    : IAgentPipeline
{
    private readonly List<IAgent> _agents = [];
    private readonly PipelineRuntime _runtime;
    private readonly IPipelineExecutionStrategy _strategy;

    public string Name { get; }

    public ParallelPipeline(string name)
        : this(
            name,
            new RuntimeEventDispatcher())
    {
    }

    internal ParallelPipeline(
        string name,
        IRuntimeEventDispatcher eventDispatcher)
        : this(
            name,
            new PipelineRuntime(eventDispatcher),
            new ParallelPipelineExecutionStrategy())
    {
    }

    internal ParallelPipeline(
        string name,
        PipelineRuntime runtime,
        IPipelineExecutionStrategy strategy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
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

        var result = await _runtime.ExecuteAsync(
            Name,
            _agents,
            context,
            _strategy,
            cancellationToken);

        return new PipelineResult(
            result.FinalOutput,
            result.Steps);
    }
}
