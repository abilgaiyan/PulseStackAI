using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class NodeExecutorResolver
    : INodeExecutorResolver
{
    private readonly IReadOnlyList<INodeExecutor> _executors;

    public NodeExecutorResolver(
        IEnumerable<INodeExecutor> executors)
    {
        _executors = executors.ToList();
    }

    public INodeExecutor Resolve(
        IPipelineNode node)
    {
        return _executors.FirstOrDefault(
                   x => x.CanExecute(node))
               ?? throw new InvalidOperationException(
                   $"No executor registered for node '{node.Name}'.");
    }
}
