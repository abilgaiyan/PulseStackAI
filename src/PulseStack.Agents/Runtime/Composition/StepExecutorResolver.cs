using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class StepExecutorResolver
    : IStepExecutorResolver
{
    private readonly IEnumerable<IStepExecutor> _executors;

    public StepExecutorResolver(
        IEnumerable<IStepExecutor> executors)
    {
       _executors = executors
            ?? throw new ArgumentNullException(
                nameof(executors));
    }

    public IStepExecutor Resolve(
        IWorkflowStep step)
    {
        return _executors.FirstOrDefault(
                   x => x.CanExecute(step))
               ?? throw new InvalidOperationException(
                   $"No executor registered for step '{step.Name}'.");
    }
}
