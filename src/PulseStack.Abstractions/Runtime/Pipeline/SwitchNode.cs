using  PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Runtime.Pipeline;

public sealed class SwitchNode
    : IPipelineNode
{
    public string Name { get; }

    public Func<PipelineContext, string?> Selector { get; }

    public IReadOnlyList<SwitchCase> Cases { get; }

    public IPipelineNode? DefaultNode { get; }

    public SwitchNode(
        string name,
        Func<PipelineContext, string?> selector,
        IEnumerable<SwitchCase> cases,
        IPipelineNode? defaultNode = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(cases);

        Name = name;
        Selector = selector;
        Cases = cases.ToList();
        DefaultNode = defaultNode;
    }
}