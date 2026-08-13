using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

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
