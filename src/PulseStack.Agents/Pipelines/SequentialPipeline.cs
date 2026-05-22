using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime;

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

    public SequentialPipeline(string name)
        : this(
            name,
            new PipelineRuntime(),
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

        _runtime =
            runtime
            ?? throw new ArgumentNullException(nameof(runtime));

        _strategy =
            strategy
            ?? throw new ArgumentNullException(nameof(strategy));
    }

    public SequentialPipeline Add(
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
            result.Steps);
    }
}