using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class ConditionalNodeExecutor
    : INodeExecutor
{
    private readonly INodeExecutorResolver _resolver;

    public ConditionalNodeExecutor(
        INodeExecutorResolver resolver)
    {
        _resolver =
            resolver
            ?? throw new ArgumentNullException(
                nameof(resolver));
    }

    public bool CanExecute(
        IPipelineNode node)
        => node is ConditionalNode;

    public async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        var conditionalNode =
            (ConditionalNode)node;

        var shouldExecute =
            await conditionalNode.Condition.EvaluateAsync(
                context,
                cancellationToken);

        if (!shouldExecute)
        {
            return new NodeExecutionResult
            {
                NodeName = conditionalNode.Name,
                Success = true
            };
        }

        var executor =
            _resolver.Resolve(
                conditionalNode.Node);

        return await executor.ExecuteAsync(
            conditionalNode.Node,
            context,
            cancellationToken);
    }
}