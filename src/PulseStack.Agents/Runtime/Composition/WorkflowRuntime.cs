using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class WorkflowRuntime
    : IWorkflowRuntime
{
    private readonly IReadOnlyList<IStepExecutor> _executors;
    private readonly IRuntimeEventDispatcher _eventDispatcher;

    public WorkflowRuntime(
        IEnumerable<IStepExecutor> executors,
        IRuntimeEventDispatcher eventDispatcher)
    {
        _executors = executors.ToList();
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<WorkflowExecutionResult> ExecuteAsync(
        Workflow workflow,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {

        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(context);

        var executionId = Guid.NewGuid();

        var startedAt = DateTimeOffset.UtcNow;

       
        _eventDispatcher.Dispatch(
            new WorkflowStartedEvent(
                executionId,
                startedAt,
                workflow.Name,
                workflow.Steps.Count));

        var results =
            new List<StepExecutionResult>();

        foreach (var step in workflow.Steps)
        {
            var executor =
                _executors.FirstOrDefault(
                    x => x.CanExecute(step));

            if (executor is null)
            {
                throw new InvalidOperationException(
                    $"No executor registered for step '{step.Name}'.");
            }

            _eventDispatcher.Dispatch(
                new StepstartedEvent(
                    executionId,
                    DateTimeOffset.UtcNow,
                    step.Name,
                    step.GetType().Name));

            var result =
                await executor.ExecuteAsync(
                    step,
                    context,
                    cancellationToken);

            _eventDispatcher.Dispatch(
                new StepCompletedEvent(
                    executionId,
                    DateTimeOffset.UtcNow,
                    step.Name,
                    step.GetType().Name,
                    result.Success));

            results.Add(result);                    
        }

        var success =  results.All(x => x.Success);
        var completedAt = DateTimeOffset.UtcNow;

        _eventDispatcher.Dispatch(
            new WorkflowCompletedEvent(
                executionId,
                completedAt,
                workflow.Name,
                success));

        return new WorkflowExecutionResult
        {
            Success = success,

            FinalOutput = context.CurrentOutput ?? string.Empty,

            Steps = results
        };
    }
}
