using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class StepExecutorResolver
    : IStepExecutorResolver
{
    private readonly IWorkflowStepExecutorSet _executorSet;

    public StepExecutorResolver(
        IEnumerable<IStepExecutor> executors)
        : this(new FixedWorkflowStepExecutorSet(executors))
    {
    }

    internal StepExecutorResolver(
        IWorkflowStepExecutorSet executorSet)
    {
        _executorSet = executorSet
            ?? throw new ArgumentNullException(nameof(executorSet));
    }

    public IStepExecutor Resolve(
        IWorkflowStep step)
    {
        return _executorSet.GetExecutors().FirstOrDefault(
                   x => x.CanExecute(step))
               ?? throw new InvalidOperationException(
                   $"No executor registered for step '{step.Name}'.");
    }
}
