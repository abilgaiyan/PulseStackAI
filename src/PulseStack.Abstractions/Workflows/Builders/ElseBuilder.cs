using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Workflows.Conditions;

namespace PulseStack.Abstractions.Workflows.Builders;

/// <summary>
/// Represents the optional Else branch of a conditional workflow.
/// This builder opens a new workflow scope where alternative business
/// actions can be authored.
/// </summary>
public sealed class ElseBuilder<TParent>
    : CompositeWorkflowBuilder<TParent>
    where TParent : IWorkflowBuilderParent<TParent>
{
    private readonly ICondition _condition;
    private readonly IReadOnlyList<IWorkflowStep> _thenSteps;

    internal ElseBuilder(
        TParent parent,
        ICondition condition,
        IReadOnlyList<IWorkflowStep> thenSteps)
        : base(parent)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(thenSteps);

        _condition = condition;
        _thenSteps = thenSteps;
    }

    public override TParent End()
    {
        throw new NotImplementedException();
    }
}