using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime;

internal interface IAgentExecutionRuntime
{
    Task<AgentExecutionResult> ExecuteAsync(
        IAgent agent,
        PipelineContext context,
        AgentExecutionContext executionContext,
        PipelineExecutionPolicy policy,
        CancellationToken cancellationToken = default);
}
