using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Abstractions.Agents;

public interface IAgentPipeline
{
    string Name { get; }

    Task<PipelineResult> RunAsync(
        string input,
        CancellationToken cancellationToken = default);

    Task<PipelineResult> RunAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default);

    Task<PipelineExecutionResult> RunDetailedAsync(
        string input,
        CancellationToken cancellationToken = default);

    Task<PipelineExecutionResult> RunDetailedAsync(
        PipelineContext context,
        CancellationToken cancellationToken = default);
}
