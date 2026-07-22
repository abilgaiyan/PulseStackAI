using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;

namespace PulseStack.Agents.Runtime.Composition;

internal abstract class CompositeStepExecutor
    : IStepExecutor
{
    protected IStepExecutorResolver Resolver { get; }

    protected CompositeStepExecutor(
        IStepExecutorResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);

        Resolver = resolver;
    }

    public abstract bool CanExecute(
        IWorkflowStep step);

    public abstract Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default);

    protected async Task<StepExecutionResult> ExecuteStepAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var executor =
            Resolver.Resolve(step);

        return await executor.ExecuteAsync(
            step,
            context,
            cancellationToken);
    }
}
