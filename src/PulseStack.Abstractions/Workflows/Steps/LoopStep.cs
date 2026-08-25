using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Workflows.Steps;

public sealed class LoopStep
    : IWorkflowStep
{
    public WorkflowStepId Id { get; }

    public string Name { get; }

    public Func<PipelineContext, IEnumerable<object>> Items { get; }

    public IWorkflowStep Step { get; }

    public IReadOnlyList<IWorkflowStep> Children => [Step];

    public LoopStep(
        string name,
        Func<PipelineContext, IEnumerable<object>> items,
        IWorkflowStep step)
        : this(
            WorkflowStepId.New(),
            name,
            items,
            step)
    {
    }

    public LoopStep(
        WorkflowStepId id,
        string name,
        Func<PipelineContext, IEnumerable<object>> items,
        IWorkflowStep step)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(step);

        Id = id;
        Name = name;
        Items = items;
        Step = step;
    }
}
