using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Workflows;

internal sealed class DefaultNodeExecutor
    : INodeExecutor
{
    public bool CanExecute(
        IPipelineNode node)
        => node is IAgent;

    public async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        if (node is not IAgent agent)
        {
            throw new InvalidOperationException(
                $"Unsupported node type '{node.GetType().Name}'.");
        }

        var startedAt =
            DateTimeOffset.UtcNow;

        var response =
            await agent.RunAsync(
                context,
                cancellationToken);

        var completedAt =
            DateTimeOffset.UtcNow;

        return new NodeExecutionResult
        {
            NodeName = node.Name,
            Success = true,
            Output = response.Text,
            Usage = null
        };
    }
}
