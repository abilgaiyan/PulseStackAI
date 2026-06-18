using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Agents;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class WorkflowNodeExecutor
    : INodeExecutor
{
    private readonly IWorkflowRuntime _workflowRuntime;

    public WorkflowNodeExecutor(
        IWorkflowRuntime workflowRuntime)
    {
        _workflowRuntime =
            workflowRuntime
            ?? throw new ArgumentNullException(
                nameof(workflowRuntime));
    }

    public bool CanExecute(
        IPipelineNode node)
        => node is WorkflowPipeline;

    public async Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(context);

        var workflow =
            (WorkflowPipeline)node;

        var result =
            await _workflowRuntime.ExecuteAsync(
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