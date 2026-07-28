namespace PulseStack.Abstractions.WorkflowPackages.Validation;

public interface IWorkflowPackageValidator
{
    ValueTask<WorkflowPackageValidationResult> ValidateAsync(
        WorkflowPackage package,
        CancellationToken cancellationToken = default);
}