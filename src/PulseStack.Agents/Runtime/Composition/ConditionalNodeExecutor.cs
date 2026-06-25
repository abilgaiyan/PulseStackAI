using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class ConditionalNodeExecutor
    : CompositeNodeExecutor
{
    public ConditionalNodeExecutor(
        INodeExecutorResolver resolver)
        : base(resolver)
    {
    }

    public override bool CanExecute(
        IPipelineNode node)
        => node is ConditionalNode;

    public override async Task<NodeExecutionResult> ExecuteAsync(
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

        return await ExecuteNodeAsync(
            conditionalNode.Node,
            context,
            cancellationToken);
    }
}
