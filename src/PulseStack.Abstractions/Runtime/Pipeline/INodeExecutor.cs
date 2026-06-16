using PulseStack.Abstractions.Agents;

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