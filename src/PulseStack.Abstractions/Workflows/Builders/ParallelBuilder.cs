using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Abstractions.Workflows.Builders;

/// Represents a Parallel language scope.
public sealed class ParallelBuilder
    : CompositeWorkflowBuilder<ParallelBuilder, WorkflowBuilder>
{
    private readonly string _parallelName;

    public ParallelBuilder(
        WorkflowBuilder parent,
        string name)
        : base(parent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _parallelName = name;
    }

    /// <summary>
    /// Compiles the authored language scope into a ParallelStep
    /// and returns to the parent workflow scope.
    /// </summary>
    public override WorkflowBuilder End()
    {
        return Parent.AddStep(
            CompileParallel(_parallelName));
    }
}
