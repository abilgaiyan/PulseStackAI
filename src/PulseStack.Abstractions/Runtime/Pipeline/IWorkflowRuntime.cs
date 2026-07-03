using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflow.Nodes;

namespace PulseStack.Abstractions.Runtime.Pipeline;
public interface IWorkflowRuntime
{
    Task<WorkflowExecutionResult> ExecuteAsync(
        WorkflowDefinition workflow,
        PipelineContext context,
        CancellationToken cancellationToken = default);
}