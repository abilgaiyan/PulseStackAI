namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record SwitchStepDocument : WorkflowStepDocument
{
    private readonly StructuralReadOnlyList<SwitchCaseDocument> cases;

    public SwitchStepDocument(
        string stepId,
        string name,
        WorkflowValueDocument selector,
        IEnumerable<SwitchCaseDocument>? cases = null,
        WorkflowStepDocument? defaultStep = null)
        : base(WorkflowStepDocumentKind.Switch, stepId)
    {
        Name = name;
        Selector = selector;
        this.cases = new StructuralReadOnlyList<SwitchCaseDocument>(cases);
        DefaultStep = defaultStep;
    }

    public string Name { get; }

    public WorkflowValueDocument Selector { get; }

    public IReadOnlyList<SwitchCaseDocument> Cases => cases;

    public WorkflowStepDocument? DefaultStep { get; }
}
