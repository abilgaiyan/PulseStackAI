using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflow.Nodes;

namespace PulseStack.Abstractions.Runtime.Pipeline;
public interface INodeExecutor
{
    bool CanExecute(
        IPipelineNode node);

    Task<NodeExecutionResult> ExecuteAsync(
        IPipelineNode node,
        PipelineContext context,
        CancellationToken cancellationToken = default);
}