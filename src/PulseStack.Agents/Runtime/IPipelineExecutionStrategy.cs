using PulseStack.Abstractions.Agents;

namespace PulseStack.Agents.Runtime;

internal interface IPipelineExecutionStrategy
{
    Task<PipelineExecutionState> ExecuteAsync(
        string pipelineName,
        IReadOnlyList<IAgent> agents,
        PipelineContext context,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}

