using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Usage;

namespace PulseStack.Agents.Runtime.Composition;

internal sealed class WorkflowStepExecutor
    : IStepExecutor
{
    private readonly Lazy<IWorkflowRuntime> _workflowRuntime;

    public WorkflowStepExecutor(
        Lazy<IWorkflowRuntime> workflowRuntime)
    {
        _workflowRuntime = workflowRuntime;
    }

    public bool CanExecute(
        IWorkflowStep step)
        => step is Workflow;

    public async Task<StepExecutionResult> ExecuteAsync(
        IWorkflowStep step,
        PipelineContext context,
        CancellationToken cancellationToken = default)
    {
        var workflow =
            (Workflow)step;

        var result =
            await _workflowRuntime.Value.ExecuteAsync(
                workflow,
                context,
                cancellationToken);

        return new StepExecutionResult
        {
            StepName = workflow.Name,
            Success = result.Success,
            Output = result.FinalOutput,
            Usage =
                result.Steps.Any(x => x.Usage is not null)
                    ? new UsageAggregator()
                        .Aggregate(
                            result.Steps.Select(x => x.Usage))
                    : null
        };
    }
}
