using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Nodes;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class LoopNodeExecutor
    : CompositeNodeExecutor
{
    public LoopNodeExecutor(
        INodeExecutorResolver resolver)
        : base(resolver)
    {
    }

    public override bool CanExecute(
        IPipelineNode node)
        => node is LoopNode;

    public override async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        var loopNode = (LoopNode)node;

        NodeExecutionResult? lastResult = null;

        foreach (var item in loopNode.Items(context))
        {
            context.Items["CurrentItem"] = item;

            lastResult =
                await ExecuteNodeAsync(
                    loopNode.Node,
                    context,
                    cancellationToken);

            if (!lastResult.Success)
            {
                return new NodeExecutionResult
                {
                    NodeName = loopNode.Name,
                    Success = lastResult.Success,
                    Output = lastResult.Output,
                    Usage = lastResult.Usage
                };
            }
        }

        return new NodeExecutionResult
        {
            NodeName = loopNode.Name,
            Success = lastResult?.Success ?? true,
            Output = lastResult?.Output ?? context.CurrentOutput,
            Usage = lastResult?.Usage
        };
    }
}
