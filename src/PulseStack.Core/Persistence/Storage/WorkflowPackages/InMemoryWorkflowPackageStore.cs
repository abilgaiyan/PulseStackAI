using PulseStack.Abstractions.WorkflowPackages.Identity;
using PulseStack.Abstractions.Persistence.Storage;
using PulseStack.Core.Persistence.Storage.Infrastructure;

namespace PulseStack.Core.Persistence.Storage.WorkflowPackages;

public sealed class InMemoryWorkflowPackageStore : IWorkflowPackageStore
{
    private readonly InMemoryStreamStore<WorkflowPackageId> _store = new();

    public ValueTask SaveAsync(
        WorkflowPackageId workflowPackageId,
        Stream input,
        CancellationToken cancellationToken = default)
    {
        workflowPackageId.EnsureValid();

        return _store.SaveAsync(
            workflowPackageId,
            input,
            cancellationToken);
    }

    public ValueTask<Stream?> LoadAsync(
        WorkflowPackageId workflowPackageId,
        CancellationToken cancellationToken = default)
    {
        workflowPackageId.EnsureValid();

        return _store.LoadAsync(
            workflowPackageId,
            cancellationToken);
    }

    public ValueTask DeleteAsync(
        WorkflowPackageId workflowPackageId,
        CancellationToken cancellationToken = default)
    {
        workflowPackageId.EnsureValid();

        return _store.DeleteAsync(
            workflowPackageId,
            cancellationToken);
    }

    public ValueTask<bool> ExistsAsync(
        WorkflowPackageId workflowPackageId,
        CancellationToken cancellationToken = default)
    {
        workflowPackageId.EnsureValid();

        return _store.ExistsAsync(
            workflowPackageId,
            cancellationToken);
    }
}