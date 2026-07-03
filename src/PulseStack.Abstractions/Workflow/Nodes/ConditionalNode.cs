using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Conditions;

namespace PulseStack.Abstractions.Workflow.Nodes;

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
