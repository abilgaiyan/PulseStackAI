using PulseStack.Abstractions.Persistence.Storage;
using PulseStack.Abstractions.Workflows;
using PulseStack.Core.Persistence.Storage.Infrastructure;

namespace PulseStack.Core.Persistence.Storage.Workflows;

public sealed class InMemoryWorkflowStore : IWorkflowStore
{
    private readonly InMemoryStreamStore<WorkflowId> _store = new();

    public ValueTask SaveAsync(
        WorkflowId workflowId,
        Stream input,
        CancellationToken cancellationToken = default)
    {
        workflowId.EnsureValid();

        return _store.SaveAsync(
            workflowId,
            input,
            cancellationToken);
    }

    public ValueTask<Stream?> LoadAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        workflowId.EnsureValid();

        return _store.LoadAsync(
            workflowId,
            cancellationToken);
    }

    public ValueTask DeleteAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        workflowId.EnsureValid();

        return _store.DeleteAsync(
            workflowId,
            cancellationToken);
    }

    public ValueTask<bool> ExistsAsync(
        WorkflowId workflowId,
        CancellationToken cancellationToken = default)
    {
        workflowId.EnsureValid();

        return _store.ExistsAsync(
            workflowId,
            cancellationToken);
    }
}