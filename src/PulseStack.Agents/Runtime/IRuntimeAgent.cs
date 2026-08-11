using PulseStack.Abstractions.Agents;

namespace PulseStack.Agents.Runtime;
internal interface IRuntimeAgent
{
    Task<AgentResponse> RunAsync(
        PipelineContext context,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}