using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Nodes;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class PipelineNodeExecutor
    : INodeExecutor
{
    public bool CanExecute(
        IPipelineNode node)
        => node is IAgentPipeline;

    public async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        if (node is not IAgentPipeline pipeline)
        {
            throw new InvalidOperationException(
                $"Unsupported node type '{node.GetType().Name}'.");
        }

        var result =
            await pipeline.RunDetailedAsync(
                context,
                cancellationToken);

        return new NodeExecutionResult
        {
            NodeName = pipeline.Name,
            Success = result.Success,
            Output = result.FinalOutput,
            Usage = result.TotalUsage
        };
    }
}
