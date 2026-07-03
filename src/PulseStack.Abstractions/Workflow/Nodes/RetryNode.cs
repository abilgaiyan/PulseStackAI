using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Abstractions.Workflow.Nodes;
public sealed class RetryNode : IPipelineNode
{
    public string Name { get; }

    public IPipelineNode Node { get; }

    public int MaxAttempts { get; }

    public RetryNode(
        string name,
        IPipelineNode node,
        int maxAttempts = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(node);

        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts));
        }

        Name = name;
        Node = node;
        MaxAttempts = maxAttempts;
    }
}