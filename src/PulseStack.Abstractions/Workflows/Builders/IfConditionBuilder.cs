using PulseStack.Abstractions.Workflows.Conditions;

namespace PulseStack.Abstractions.Workflows.Builders;

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
    private readonly string _name;

    internal IfConditionBuilder(
        TParent parent,
         string name,
        ICondition condition)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(condition);

        _parent = parent;
          _name = name;
        _condition = condition;
    }

    public ThenBuilder<TParent> Then()
    {
        return new ThenBuilder<TParent>(
            _parent,
            _name,
            _condition);
    }
}