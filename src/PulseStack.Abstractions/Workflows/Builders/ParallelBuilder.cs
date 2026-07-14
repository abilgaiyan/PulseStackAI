using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Abstractions.Workflows.Builders;

public sealed class ParallelBuilder
    : CompositeWorkflowBuilder<ParallelBuilder, WorkflowBuilder>
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

        return Parent.AddStep(
            CompileParallel(_name));
    }
}
