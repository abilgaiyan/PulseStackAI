using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Runtime.Pipeline;
public interface IWorkflowRuntime
{
    Task<WorkflowExecutionResult> ExecuteAsync(
        Workflow workflow,
        PipelineContext context,
        CancellationToken cancellationToken = default);
}