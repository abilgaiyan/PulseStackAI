using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Nodes;

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
                return new NodeExecutionResult
                {
                    NodeName = retryNode.Name,
                    Success = lastResult.Success,
                    Output = lastResult.Output,
                    Usage = lastResult.Usage
                };
            }
        }

        return new NodeExecutionResult
        {
            NodeName = retryNode.Name,
            Success = lastResult!.Success,
            Output = lastResult.Output,
            Usage = lastResult.Usage
        };
    }        
    
}
