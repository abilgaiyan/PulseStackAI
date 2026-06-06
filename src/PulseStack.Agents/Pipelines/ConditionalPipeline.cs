using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Pipelines;

public sealed class ConditionalPipeline
    : IAgentPipeline
{
    public string Name => throw new NotImplementedException();

    public Task<PipelineResult> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<PipelineResult> RunAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<PipelineExecutionResult> RunDetailedAsync(string input, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<PipelineExecutionResult> RunDetailedAsync(PipelineContext context, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}