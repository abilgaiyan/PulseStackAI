using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Common.Identity;

namespace PulseStack.Abstractions.Workflows.Steps;

public sealed class ConditionalStep : IWorkflowStep
{
    public WorkflowStepId Id { get; } = WorkflowStepId.New();
    public string Name { get; }

    public ICondition Condition { get; }

    public IWorkflowStep ThenStep { get; }

    public IWorkflowStep? ElseStep { get; }

    public IReadOnlyList<IWorkflowStep> Children =>
        ElseStep is null
            ? [ThenStep]
            : [ThenStep, ElseStep];

    public ConditionalStep(
        string name,
        ICondition condition,
        IWorkflowStep thenStep,
        IWorkflowStep? elseStep = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(thenStep);

        Name = name;
        Condition = condition;
        ThenStep = thenStep;
        ElseStep = elseStep;
    }
}
