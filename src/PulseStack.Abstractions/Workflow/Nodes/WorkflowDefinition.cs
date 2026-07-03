using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Abstractions.Workflow.Nodes;

/// <summary>
/// Represents a composable workflow
/// consisting of pipeline nodes.
/// </summary>
public sealed class WorkflowDefinition : IPipelineNode
{
    private readonly List<IPipelineNode> _nodes = [];

    public string Name { get; }

    public IReadOnlyList<IPipelineNode> Nodes =>
        _nodes;

    public WorkflowDefinition(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
    }

    public WorkflowDefinition Add(
        IPipelineNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        _nodes.Add(node);

        return this;
    }
}
