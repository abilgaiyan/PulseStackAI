using PulseStack.Abstractions.Agents;

namespace PulseStack.Agents.Runtime;

/// <summary>
/// Internal execution boundary for realized agents.
/// </summary>
internal interface IRuntimeAgentExecutor 
{
    Task<AgentResponse> RunAsync(
        PipelineContext context,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}
