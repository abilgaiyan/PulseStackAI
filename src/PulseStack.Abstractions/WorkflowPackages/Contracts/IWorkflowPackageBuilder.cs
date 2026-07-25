using PulseStack.Abstractions.WorkflowPackages;

namespace PulseStack.Abstractions.WorkflowPackages.Contracts;

public interface IWorkflowPackageBuilder
{
    ValueTask<Stream> BuildAsync(
        WorkflowPackage package,
        CancellationToken cancellationToken = default);
}