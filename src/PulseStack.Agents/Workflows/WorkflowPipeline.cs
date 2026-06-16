using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Workflows;

/// <summary>
/// Represents a composable workflow
/// consisting of pipeline nodes.
/// </summary>
public sealed class WorkflowPipeline
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
