namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record RunStepDocument : WorkflowStepDocument
{
    public RunStepDocument(
        string stepId,
        AIAssetReferenceDocument agent)
        : base(WorkflowStepDocumentKind.Run, stepId)
    {
        Agent = agent;
    }

    public AIAssetReferenceDocument Agent { get; }
}
