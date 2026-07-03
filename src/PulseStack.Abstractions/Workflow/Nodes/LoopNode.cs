using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Abstractions.Workflow.Nodes;

public sealed class LoopNode
    : IPipelineNode
{
    public string Name { get; }

    public Func<PipelineContext, IEnumerable<object>> Items { get; }

    public IPipelineNode Node { get; }

    public LoopNode(
        string name,
        Func<PipelineContext, IEnumerable<object>> items,
        IPipelineNode node)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(node);

        Name = name;
        Items = items;
        Node = node;
    }
}
