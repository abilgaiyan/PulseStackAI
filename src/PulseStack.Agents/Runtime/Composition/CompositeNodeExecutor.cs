using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Nodes;

namespace PulseStack.Agents.Runtime.Composition;

internal abstract class CompositeNodeExecutor
    : INodeExecutor
{
    private readonly INodeExecutorResolver _resolver;

    protected CompositeNodeExecutor(
        INodeExecutorResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        _resolver = resolver;
    }

    public abstract bool CanExecute(
        IPipelineNode node);

    public abstract Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default);

    protected async Task<NodeExecutionResult> ExecuteNodeAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var executor =
            _resolver.Resolve(node);

        return await executor.ExecuteAsync(
            node,
            context,
            cancellationToken);
    }
}
