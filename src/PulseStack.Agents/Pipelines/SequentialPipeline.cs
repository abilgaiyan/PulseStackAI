using PulseStack.Abstractions.Agents;

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

    public string Name { get; }

    public SequentialPipeline(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
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

        if (_agents.Count == 0)
        {
            throw new InvalidOperationException(
                "Pipeline contains no agents.");
        }

        var steps = new List<PipelineStepResult>();

        foreach (var agent in _agents)
        {
            var input = context.CurrentOutput;

            var response = await agent.RunAsync(
                input,
                cancellationToken);

            var output = response.Text ?? string.Empty;

            steps.Add(new PipelineStepResult(
                agent.Name,
                agent.Model,
                input,
                output));

            context.CurrentOutput = output;
        }

        return new PipelineResult(
            context.CurrentOutput,
            steps);
    }
}