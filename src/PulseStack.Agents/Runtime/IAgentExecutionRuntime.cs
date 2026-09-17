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

internal sealed class AgentExecutionRuntime : IAgentExecutionRuntime
{
    private readonly AgentRuntime _agentRuntime;

    internal AgentExecutionRuntime(AgentRuntime agentRuntime)
    {
        _agentRuntime = agentRuntime ?? throw new ArgumentNullException(nameof(agentRuntime));
    }

    public Task<AgentExecutionResult> ExecuteAsync(
        IAgent agent,
        PipelineContext context,
        AgentExecutionContext executionContext,
        PipelineExecutionPolicy policy,
        CancellationToken cancellationToken = default)
        => _agentRuntime.ExecuteAsync(
            agent,
            context,
            executionContext,
            policy,
            cancellationToken);
}
