using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class WorkflowRuntime
    : IWorkflowRuntime
{
    private readonly IReadOnlyList<INodeExecutor> _executors;

    public WorkflowRuntime(
        IEnumerable<INodeExecutor> executors)
    {
        _executors = executors.ToList();
    }

    public async Task<WorkflowExecutionResult> ExecuteAsync(
        WorkflowPipeline workflow,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
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

            var result =
                await executor.ExecuteAsync(
                    node,
                    context,
                    cancellationToken);

            results.Add(result);
        }

        return new WorkflowExecutionResult
        {
            Success =
                results.All(x => x.Success),

            FinalOutput =
                context.CurrentOutput
                ?? string.Empty,

            Nodes =
                results
        };
    }
}
