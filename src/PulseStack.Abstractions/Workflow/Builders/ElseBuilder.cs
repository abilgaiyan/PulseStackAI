using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Conditions;

namespace PulseStack.Abstractions.Workflow.Builders;

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
    private readonly IReadOnlyList<IPipelineNode> _thenNodes;

    internal ElseBuilder(
        TParent parent,
        ICondition condition,
        IReadOnlyList<IPipelineNode> thenNodes)
        : base(parent)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(thenNodes);

        _condition = condition;
        _thenNodes = thenNodes;
    }

    public override TParent End()
    {
        throw new NotImplementedException();
    }
}