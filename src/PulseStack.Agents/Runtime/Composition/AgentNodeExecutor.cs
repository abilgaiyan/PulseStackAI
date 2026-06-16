using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
namespace PulseStack.Agents.Runtime.Composition;

internal sealed class AgentNodeExecutor
    : INodeExecutor
{
    private readonly AgentRuntime _agentRuntime;

    public AgentNodeExecutor(
        AgentRuntime agentRuntime)
    {
        _agentRuntime = agentRuntime;
    }

    public bool CanExecute(
        IPipelineNode node)
        => node is IAgent;

    public async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var agent =
            (IAgent)node;

        var executionContext =
            new AgentExecutionContext(
                context,
                [],
                cancellationToken);

        var result =
            await _agentRuntime.ExecuteAsync(
                agent,
                context,
                executionContext,
                new PipelineExecutionPolicy(),
                cancellationToken);

        return new NodeExecutionResult
        {
            NodeName = agent.Name,
            Success = result.Success,
            Output = result.Output,
            Usage = result.Usage
        };
    }
}
