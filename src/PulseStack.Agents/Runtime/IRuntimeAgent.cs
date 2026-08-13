using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime;

/// <summary>
/// Compatibility bridge for the existing AgentRuntime execution path.
/// </summary>
[Obsolete("Use IRuntimeAgentExecutor instead.")]
internal interface IRuntimeAgent
{
    Task<AgentResponse> RunAsync(
        PipelineContext context,
        AgentExecutionContext executionContext,
        CancellationToken cancellationToken = default);
}
