using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Runtime.Pipeline;

public sealed class ConditionalNode
    : IPipelineNode
{
    public string Name { get; }

    public ICondition Condition { get; }

    public IPipelineNode Node { get; }

    public ConditionalNode(
        string name,
        ICondition condition,
        IPipelineNode node)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(node);

        Name = name;
        Condition = condition;
        Node = node;
    }
}
