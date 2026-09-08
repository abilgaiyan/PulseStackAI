namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record LoopStepDocument : WorkflowStepDocument
{
    public LoopStepDocument(
        string stepId,
        string name,
        WorkflowValueDocument items,
        WorkflowStepDocument step)
        : base(WorkflowStepDocumentKind.Loop, stepId)
    {
        Name = name;
        Items = items;
        Step = step;
    }

    public string Name { get; }

    public WorkflowValueDocument Items { get; }

    public WorkflowStepDocument Step { get; }
}
