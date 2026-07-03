using PulseStack.Abstractions.Workflow.Nodes;

namespace PulseStack.Abstractions.Workflow.Builders;

public sealed class ParallelBuilder
    : CompositeWorkflowBuilder<WorkflowBuilder>
{
    private readonly string _name;

    public ParallelBuilder(
        WorkflowBuilder parent,
        string name)
        : base(parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _name = name;
    }

    public override WorkflowBuilder End()
    {
        if (Nodes.Count == 0)
        {
            throw new InvalidOperationException(
                "Parallel block requires at least one node.");
        }

        var parallel = new ParallelNode(_name);

        foreach (var node in Nodes)
        {
            parallel.Add(node);
        }

        return Parent.AddNode(parallel);
    }
}
