using PulseStack.Abstractions.WorkflowPackages;

namespace PulseStack.Abstractions.WorkflowPackages.Contracts;
public interface IWorkflowPackageReader
{
    ValueTask<WorkflowPackage> ReadAsync(
        Stream package,
        CancellationToken cancellationToken = default);
}