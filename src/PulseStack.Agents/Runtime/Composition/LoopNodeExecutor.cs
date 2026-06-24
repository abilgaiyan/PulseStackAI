using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class LoopNodeExecutor
    : INodeExecutor
{
    private readonly INodeExecutorResolver _resolver;

    public LoopNodeExecutor(
        INodeExecutorResolver resolver)
    {
        _resolver = resolver;
    }

    public bool CanExecute(
        IPipelineNode node)
        => node is LoopNode;

    public async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        var loopNode = (LoopNode)node;

        var executor =
            _resolver.Resolve(loopNode.Node);

        NodeExecutionResult? lastResult = null;

        foreach (var item in loopNode.Items(context))
        {
            context.Items["CurrentItem"] = item;

            lastResult =
                await executor.ExecuteAsync(
                    loopNode.Node,
                    context,
                    cancellationToken);

            if (!lastResult.Success)
            {
                return new NodeExecutionResult
                {
                    NodeName = loopNode.Name,
                    Success = false,
                    Output = context.CurrentOutput,
                    Usage = lastResult.Usage
                };
            }
        }

        return new NodeExecutionResult
        {
            NodeName = loopNode.Name,
            Success = lastResult?.Success ?? true,
            Output = context.CurrentOutput,
            Usage = lastResult?.Usage
        };
    }
}
