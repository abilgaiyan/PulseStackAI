using PulseStack.Abstractions.Workflow.Conditions;

namespace PulseStack.Abstractions.Workflow.Builders;


/// <summary>
/// Represents the grammar transition immediately following an If statement.
///
/// This builder does not author workflow steps.
/// Its only responsibility is to guide the developer to the next
/// valid language construct: Then().
/// </summary>
public sealed class IfConditionBuilder<TParent>
    where TParent : IWorkflowBuilderParent<TParent>
{
    private readonly TParent _parent;
    private readonly ICondition _condition;

    internal IfConditionBuilder(
        TParent parent,
        ICondition condition)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(condition);

        _parent = parent;
        _condition = condition;
    }

    public ThenBuilder<TParent> Then()
    {
        return new ThenBuilder<TParent>(
            _parent,
            _condition);
    }
}