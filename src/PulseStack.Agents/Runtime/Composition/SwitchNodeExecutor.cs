using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class SwitchNodeExecutor
    : INodeExecutor
{
    private readonly INodeExecutorResolver _resolver;

    public SwitchNodeExecutor(
        INodeExecutorResolver resolver)
    {
        _resolver =
            resolver
            ?? throw new ArgumentNullException(
                nameof(resolver));
    }

    public bool CanExecute(
        IPipelineNode node)
        => node is SwitchNode;

    public async Task<NodeExecutionResult> ExecuteAsync(
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
            var executor =
                _resolver.Resolve(
                    matchedCase.Node);

            result =
                await executor.ExecuteAsync(
                    matchedCase.Node,
                    context,
                    cancellationToken);
        }
        else if (switchNode.DefaultNode is not null)
        {
            var executor =
                _resolver.Resolve(
                    switchNode.DefaultNode);

            result =
                await executor.ExecuteAsync(
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