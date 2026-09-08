namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record ConditionalStepDocument : WorkflowStepDocument
{
    public ConditionalStepDocument(
        string stepId,
        string name,
        WorkflowConditionDocument condition,
        WorkflowStepDocument thenStep,
        WorkflowStepDocument? elseStep = null)
        : base(WorkflowStepDocumentKind.Conditional, stepId)
    {
        Name = name;
        Condition = condition;
        ThenStep = thenStep;
        ElseStep = elseStep;
    }

    public string Name { get; }

    public WorkflowConditionDocument Condition { get; }

    public WorkflowStepDocument ThenStep { get; }

    public WorkflowStepDocument? ElseStep { get; }
}
