using PulseStack.Abstractions.Workflow.Conditions;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Abstractions.Workflow.Builders;

public sealed class ThenBuilder<TParent>
    : CompositeWorkflowBuilder<TParent>
    where TParent : IWorkflowBuilderParent<TParent>
{
    private readonly ICondition _condition;
    internal ThenBuilder(
        TParent parent,
        ICondition condition)
        : base(parent)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _condition = condition;
    }

    public ElseBuilder<TParent> Else()
    {
        return new ElseBuilder<TParent>(
            Parent,
            _condition,
            Nodes);
    }
    public override TParent End()
    {
        throw new NotImplementedException();
    }
}