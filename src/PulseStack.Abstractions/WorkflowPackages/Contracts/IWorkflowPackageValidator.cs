using PulseStack.Abstractions.WorkflowPackages;

namespace PulseStack.Abstractions.WorkflowPackages.Contracts;

public interface IWorkflowPackageValidator
{
    ValueTask<WorkflowPackageValidationResult> ValidateAsync(
        WorkflowPackage package,
        CancellationToken cancellationToken = default);
}