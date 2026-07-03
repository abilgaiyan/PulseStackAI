using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Nodes;

namespace PulseStack.Abstractions.Workflow.Routing;

public sealed class SwitchCase
{
    public string Value { get; }

    public IPipelineNode Node { get; }

    public SwitchCase(
        string value,
        IPipelineNode node)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        ArgumentNullException.ThrowIfNull(node);

        Value = value;
        Node = node;
    }
}