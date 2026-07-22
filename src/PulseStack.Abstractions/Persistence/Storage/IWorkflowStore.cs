using PulseStack.Abstractions.Workflows;

namespace PulseStack.Abstractions.Persistence.Storage;

public interface IWorkflowStore
{
    ValueTask SaveAsync(
        WorkflowId workflowId,
        Stream input,
        CancellationToken cancellationToken = default);

    ValueTask<Stream?> LoadAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default);

    ValueTask<bool> ExistsAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default);
}
