using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime.Composition;

internal interface IWorkflowStepExecutorSet
{
    IEnumerable<IStepExecutor> GetExecutors();
}

internal sealed class DeferredWorkflowStepExecutorSet
    : IWorkflowStepExecutorSet
{
    private readonly IServiceProvider _serviceProvider;

    public DeferredWorkflowStepExecutorSet(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider
            ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public IEnumerable<IStepExecutor> GetExecutors() =>
        _serviceProvider.GetServices<IStepExecutor>();
}

internal sealed class FixedWorkflowStepExecutorSet
    : IWorkflowStepExecutorSet
{
    private readonly IEnumerable<IStepExecutor> _executors;

    public FixedWorkflowStepExecutorSet(
        IEnumerable<IStepExecutor> executors)
    {
        _executors = executors
            ?? throw new ArgumentNullException(nameof(executors));
    }

    public IEnumerable<IStepExecutor> GetExecutors() => _executors;
}
