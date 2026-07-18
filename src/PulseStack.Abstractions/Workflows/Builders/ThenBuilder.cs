using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Workflows.Language;

namespace PulseStack.Abstractions.Workflows.Builders;

public sealed class ThenBuilder<TParent>
    : CompositeWorkflowBuilder<ThenBuilder<TParent>, TParent>
    where TParent : IWorkflowBuilderParent<TParent>
{
    private readonly ICondition _condition;
    private readonly string _name;
    internal ThenBuilder(
        TParent parent,
        string name,
        ICondition condition)
        : base(parent)
    {
        _name = name;
        ArgumentNullException.ThrowIfNull(condition);
        _condition = condition;
    }

    public ElseBuilder<TParent> Else()
    {
        return new ElseBuilder<TParent>(
            Parent,
            _name,
            _condition,
            CompileWorkflow(WorkflowKeywords.Then));
    }    
    
    public override TParent End()
    {
        if (Steps.Count == 0)
            throw new InvalidOperationException(
                "A Then block must contain at least one workflow step.");

        return Parent.AddStep(
            new ConditionalStep(
                _name,
                _condition,
                CompileWorkflow(WorkflowKeywords.Then)));
    }
}