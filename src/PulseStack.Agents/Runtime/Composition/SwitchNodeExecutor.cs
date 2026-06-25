using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class SwitchNodeExecutor
    : CompositeNodeExecutor
{
    public SwitchNodeExecutor(
        INodeExecutorResolver resolver)
        : base(resolver)
    {
    }

    public override bool CanExecute(
        IPipelineNode node)
        => node is SwitchNode;

    public override async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        var switchNode =
            (SwitchNode)node;

        var selectedValue =
            switchNode.Selector(
                context);

        var matchedCase =
            switchNode.Cases.FirstOrDefault(
                x => string.Equals(
                    x.Value,
                    selectedValue,
                    StringComparison.OrdinalIgnoreCase));


        NodeExecutionResult? result = null;

        if (matchedCase is not null)
        {
            result =
                await ExecuteNodeAsync(
                    matchedCase.Node,
                    context,
                    cancellationToken);
        }
        else if (switchNode.DefaultNode is not null)
        {
            result =
                await ExecuteNodeAsync(
                    switchNode.DefaultNode,
                    context,
                    cancellationToken);
        }

        return new NodeExecutionResult
        {
            NodeName = switchNode.Name,
            Success = result?.Success ?? true,
            Output = result?.Output ?? context.CurrentOutput,
            Usage = result?.Usage
        };
    }
}
