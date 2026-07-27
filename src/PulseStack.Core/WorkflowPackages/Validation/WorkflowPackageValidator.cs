using PulseStack.Abstractions.Persistence.Mapping;
using PulseStack.Abstractions.Persistence.Validation;
using PulseStack.Abstractions.WorkflowPackages;
using PulseStack.Abstractions.WorkflowPackages.Validation;
using PulseStack.Core.WorkflowPackages.Validation.Rules;

namespace PulseStack.Core.WorkflowPackages.Validation;

internal sealed class WorkflowPackageValidator : IWorkflowPackageValidator
{
    private readonly WorkflowValidationRule _workflowValidationRule;

    public WorkflowPackageValidator(
        IWorkflowMapper mapper,
        IWorkflowValidator workflowValidator)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(workflowValidator);

        _workflowValidationRule = new WorkflowValidationRule(
            mapper,
            workflowValidator);
    }

    public async ValueTask<WorkflowPackageValidationResult> ValidateAsync(
        WorkflowPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var context = new WorkflowPackageValidationContext(
            cancellationToken);

        PackageIdentityRule.Validate(
            package,
            context);

        PackageMetadataRule.Validate(
            package,
            context);

        RuntimeCompatibilityRule.Validate(
            package,
            context);

        await _workflowValidationRule.ValidateAsync(
            package,
            context);

        return context.ToResult();
    }
}