using PulseStack.Abstractions.WorkflowPackages.Identity;

namespace PulseStack.Abstractions.Persistence.Storage;

public interface IWorkflowPackageStore
{
    ValueTask SaveAsync(
        WorkflowPackageId packageId,
        Stream input,
        CancellationToken cancellationToken = default);

    ValueTask<Stream?> LoadAsync(
        WorkflowPackageId packageId,
        CancellationToken cancellationToken = default);

    ValueTask DeleteAsync(
        WorkflowPackageId packageId,
        CancellationToken cancellationToken = default);

    ValueTask<bool> ExistsAsync(
        WorkflowPackageId packageId,
        CancellationToken cancellationToken = default);        
}