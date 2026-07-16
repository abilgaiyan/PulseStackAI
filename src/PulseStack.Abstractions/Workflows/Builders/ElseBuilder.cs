using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Language;

namespace PulseStack.Abstractions.Workflows.Builders;

/// <summary>
/// Represents the optional Else branch of a conditional workflow.
/// This builder opens a new workflow scope where alternative business
/// actions can be authored.
/// </summary>
public sealed class ElseBuilder<TParent>
     : CompositeWorkflowBuilder<ElseBuilder<TParent>, TParent>
    where TParent : IWorkflowBuilderParent<TParent>
{
    private readonly ICondition _condition;
    private readonly Workflow _thenWorkflow;
    private readonly string _name;
    internal ElseBuilder(
        TParent parent,
        string name,
        ICondition condition,
        Workflow thenWorkflow)
        : base(parent)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(thenWorkflow);

        _name = name;
        _condition = condition;
        _thenWorkflow = thenWorkflow;
    }

   public override TParent End()
    {
        if (Steps.Count == 0)
            throw new InvalidOperationException(
                "An Else block must contain at least one workflow step.");

        return Parent.AddStep(
            new ConditionalStep(
                _name,
                _condition,
                _thenWorkflow,
                CompileWorkflow(WorkflowKeywords.Else)));
    }
}