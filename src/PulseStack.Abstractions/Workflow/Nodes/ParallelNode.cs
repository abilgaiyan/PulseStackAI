using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Abstractions.Workflow.Nodes;
public sealed class ParallelNode : IPipelineNode
{
    private readonly List<IPipelineNode> _nodes = [];

    public string Name { get; }

    public IReadOnlyList<IPipelineNode> Nodes
        => _nodes;

    public ParallelNode(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    public ParallelNode Add(
        IPipelineNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        _nodes.Add(node);

        return this;
    }
}
