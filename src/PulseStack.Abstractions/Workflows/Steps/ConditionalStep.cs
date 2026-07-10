using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Runtime.Pipeline;


namespace PulseStack.Abstractions.Workflows.Steps;

public sealed class ConditionalStep
    : IWorkflowStep
{
    public string Name { get; }

    public ICondition Condition { get; }

    public IWorkflowStep Step { get; }

    public IReadOnlyList<IWorkflowStep> Children => [];

    public ConditionalStep(
        string name,
        ICondition condition,
        IWorkflowStep step)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(step);

        Name = name;
        Condition = condition;
        Step = step;
    }
}
