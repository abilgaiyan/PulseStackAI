
namespace PulseStack.Abstractions.Runtime.Pipeline;

/// <summary>
/// Represents a composable workflow
/// consisting of pipeline nodes.
/// </summary>
public sealed class WorkflowPipeline : IPipelineNode
{
    private readonly List<IPipelineNode> _nodes = [];

    public string Name { get; }

    public IReadOnlyList<IPipelineNode> Nodes =>
        _nodes;

    public WorkflowPipeline(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    public WorkflowPipeline Add(
        IPipelineNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        _nodes.Add(node);

        return this;
    }
}
