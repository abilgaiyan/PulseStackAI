using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Nodes;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class NodeExecutorResolver
    : INodeExecutorResolver
{
    private readonly IEnumerable<INodeExecutor> _executors;

    public NodeExecutorResolver(
        IEnumerable<INodeExecutor> executors)
    {
       _executors = executors
            ?? throw new ArgumentNullException(
                nameof(executors));
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
