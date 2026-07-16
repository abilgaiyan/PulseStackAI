using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Workflows;
public sealed class RetryStep : IWorkflowStep
{
    public WorkflowStepId Id { get; } = WorkflowStepId.New();
    public string Name { get; }

    public IWorkflowStep Step { get; }

    public int MaxAttempts { get; }

    public IReadOnlyList<IWorkflowStep> Children => [Step];

    public RetryStep(
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

        Name = name;
        Step = step;
        MaxAttempts = maxAttempts;
    }
}