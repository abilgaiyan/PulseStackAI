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

    public async Task<PipelineResult> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);

        if (_agents.Count == 0)
        {
            throw new InvalidOperationException(
                "Pipeline contains no agents.");
        }

        var steps = new List<PipelineStepResult>();

        var current = input;

        foreach (var agent in _agents)
        {
            var response = await agent.RunAsync(
                current,
                cancellationToken);

            var output = response.Text ?? string.Empty;

            steps.Add(new PipelineStepResult(
                agent.Name,
                agent.Model,
                current,
                output));

            current = output;
        }

        return new PipelineResult(
            current,
            steps);
    }
}
