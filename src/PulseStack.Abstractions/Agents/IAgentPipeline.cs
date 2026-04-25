namespace PulseStack.Abstractions.Agents;

public interface IAgentPipeline
{
    string Name { get; }

    Task<PipelineResult> RunAsync(
        string input,
        CancellationToken cancellationToken = default);
}
