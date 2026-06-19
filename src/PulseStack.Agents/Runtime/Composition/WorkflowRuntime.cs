using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class WorkflowRuntime
    : IWorkflowRuntime
{
    private readonly IReadOnlyList<INodeExecutor> _executors;
    private readonly IRuntimeEventDispatcher _eventDispatcher;

    public WorkflowRuntime(
        IEnumerable<INodeExecutor> executors,
        IRuntimeEventDispatcher eventDispatcher)
    {
        _executors = executors.ToList();
        _eventDispatcher = eventDispatcher ?? throw new ArgumentNullException(nameof(eventDispatcher));
    }

    public async Task<WorkflowExecutionResult> ExecuteAsync(
        WorkflowPipeline workflow,
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
                workflow.Nodes.Count));

        var results =
            new List<NodeExecutionResult>();

        foreach (var node in workflow.Nodes)
        {
            var executor =
                _executors.FirstOrDefault(
                    x => x.CanExecute(node));

            if (executor is null)
            {
                throw new InvalidOperationException(
                    $"No executor registered for node '{node.Name}'.");
            }

            _eventDispatcher.Dispatch(
                new NodeStartedEvent(
                    executionId,
                    DateTimeOffset.UtcNow,
                    node.Name,
                    node.GetType().Name));

            var result =
                await executor.ExecuteAsync(
                    node,
                    context,
                    cancellationToken);

            _eventDispatcher.Dispatch(
                new NodeCompletedEvent(
                    executionId,
                    DateTimeOffset.UtcNow,
                    node.Name,
                    node.GetType().Name,
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

            Nodes = results
        };
    }
}
