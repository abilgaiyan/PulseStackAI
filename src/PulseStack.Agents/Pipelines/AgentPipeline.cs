using PulseStack.Abstractions.Agents;

namespace PulseStack.Agents.Pipelines;

public sealed class AgentPipeline : IAgentPipeline
{
    private readonly List<IAgent> _agents = [];

    public string Name { get; }

    public AgentPipeline(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    public AgentPipeline AddAgent(IAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        _agents.Add(agent);

        return this;
    }

    public async Task<PipelineResult> RunAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var steps = new List<PipelineStepResult>();

        var currentInput = input;

        foreach (var agent in _agents)
        {
            var result = await agent.RunAsync(
                currentInput,
                cancellationToken);

            var output = result.Text ?? string.Empty;

            steps.Add(new PipelineStepResult(
                agent.Name,
                output));

            currentInput = output;
        }

        return new PipelineResult(
            currentInput,
            steps);
    }
}