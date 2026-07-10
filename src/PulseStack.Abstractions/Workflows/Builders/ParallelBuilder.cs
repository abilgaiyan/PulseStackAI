using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Abstractions.Workflows.Builders;

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
        if (Steps.Count == 0)
        {
            throw new InvalidOperationException(
                "Parallel block requires at least one step.");
        }

        var parallel = new ParallelStep(_name);

        foreach (var step in Steps)
        {
            parallel.Add(step);
        }

        return Parent.AddStep(parallel);
    }
}
