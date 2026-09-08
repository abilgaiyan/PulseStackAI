namespace PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

public sealed record RetryStepDocument : WorkflowStepDocument
{
    public RetryStepDocument(
        string stepId,
        string name,
        WorkflowStepDocument step,
        int maxAttempts)
        : base(WorkflowStepDocumentKind.Retry, stepId)
    {
        Name = name;
        Step = step;
        MaxAttempts = maxAttempts;
    }

    public string Name { get; }

    public WorkflowStepDocument Step { get; }

    public int MaxAttempts { get; }
}
