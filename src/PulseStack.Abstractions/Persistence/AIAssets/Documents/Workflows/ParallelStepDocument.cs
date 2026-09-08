namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record ParallelStepDocument : WorkflowStepDocument
{
    private readonly StructuralReadOnlyList<WorkflowStepDocument> steps;

    public ParallelStepDocument(
        string stepId,
        string name,
        IEnumerable<WorkflowStepDocument>? steps = null)
        : base(WorkflowStepDocumentKind.Parallel, stepId)
    {
        Name = name;
        this.steps = new StructuralReadOnlyList<WorkflowStepDocument>(steps);
    }

    public string Name { get; }

    public IReadOnlyList<WorkflowStepDocument> Steps => steps;
}
