using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Workflows.Steps;

public sealed class RetryStep : IWorkflowStep
{
    public WorkflowStepId Id { get; }

    public string Name { get; }

    public IWorkflowStep Step { get; }

    public int MaxAttempts { get; }

    public IReadOnlyList<IWorkflowStep> Children => [Step];

    public RetryStep(
        string name,
        IWorkflowStep step,
        int maxAttempts = 3)
        : this(
            WorkflowStepId.New(),
            name,
            step,
            maxAttempts)
    {
    }

    public RetryStep(
        WorkflowStepId id,
        string name,
        IWorkflowStep step,
        int maxAttempts = 3)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(step);

        if (maxAttempts < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxAttempts));
        }

        Id = id;
        Name = name;
        Step = step;
        MaxAttempts = maxAttempts;
    }
}
