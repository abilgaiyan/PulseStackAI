using PulseStack.Abstractions.Agents;

namespace PulseStack.Abstractions.Runtime.Pipeline;
public interface IWorkflowRuntime
{
    Task<WorkflowExecutionResult> ExecuteAsync(
        WorkflowPipeline workflow,
        PipelineContext context,
        CancellationToken cancellationToken = default);
}