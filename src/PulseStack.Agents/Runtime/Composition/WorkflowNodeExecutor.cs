using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class WorkflowNodeExecutor
    : INodeExecutor
{
    private readonly Lazy<IWorkflowRuntime> _workflowRuntime;

    public WorkflowNodeExecutor(
        Lazy<IWorkflowRuntime> workflowRuntime)
    {
        _workflowRuntime = workflowRuntime;
    }

    public bool CanExecute(
        IPipelineNode node)
        => node is WorkflowPipeline;

    public async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var workflow =
            (WorkflowPipeline)node;

        var result =
            await _workflowRuntime.Value.ExecuteAsync(
                workflow,
                context,
                cancellationToken);

        return new NodeExecutionResult
        {
            NodeName = workflow.Name,
            Success = result.Success,
            Output = result.FinalOutput,
            Usage = null
        };
    }
}