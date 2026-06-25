using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class RetryNodeExecutor
    : CompositeNodeExecutor
{
    public RetryNodeExecutor(
        INodeExecutorResolver resolver)
        : base(resolver)
    {
    }

    public override bool CanExecute(
        IPipelineNode node)
        => node is RetryNode;

    public override async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var retryNode =
            (RetryNode)node;

        NodeExecutionResult? lastResult = null;

        for (var attempt = 1;
            attempt <= retryNode.MaxAttempts;
            attempt++)
        {
            lastResult =
                await ExecuteNodeAsync(
                    retryNode.Node,
                    context,
                    cancellationToken);

            if (lastResult.Success)
            {
                return lastResult;
            }
        }

        return lastResult!;
    }        
    
}
