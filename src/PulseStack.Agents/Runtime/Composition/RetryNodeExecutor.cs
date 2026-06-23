using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class RetryNodeExecutor
    : INodeExecutor
{
    private readonly INodeExecutorResolver _resolver;

    public RetryNodeExecutor(
        INodeExecutorResolver resolver)
    {
        _resolver = resolver;
    }

    public bool CanExecute(
        IPipelineNode node)
        => node is RetryNode;

    public async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var retryNode =
            (RetryNode)node;

        var executor =
            _resolver.Resolve(
                retryNode.Node);

        NodeExecutionResult? lastResult = null;

        for (var attempt = 1;
            attempt <= retryNode.MaxAttempts;
            attempt++)
        {
            lastResult =
                await executor.ExecuteAsync(
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