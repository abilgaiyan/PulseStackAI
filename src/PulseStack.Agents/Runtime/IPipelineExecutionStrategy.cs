using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime;

internal interface IPipelineExecutionStrategy
{
    Task<PipelineExecutionState> ExecuteAsync(
        string pipelineName,
        IReadOnlyList<IAgent> agents,
        PipelineContext context,
        AgentExecutionContext executionContext,
        PipelineExecutionPolicy policy,
        CancellationToken cancellationToken = default);
}

