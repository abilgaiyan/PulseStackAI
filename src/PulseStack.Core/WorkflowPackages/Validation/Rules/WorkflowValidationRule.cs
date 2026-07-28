using PulseStack.Abstractions.WorkflowPackages;
using PulseStack.Abstractions.Persistence.Mapping;
using PulseStack.Abstractions.Persistence.Validation;
using PulseStack.Abstractions.WorkflowPackages.Validation;

namespace PulseStack.Core.WorkflowPackages.Validation.Rules;
internal sealed class WorkflowValidationRule
{
    private readonly IWorkflowMapper _mapper;
    private readonly IWorkflowValidator _validator;

    public WorkflowValidationRule(
        IWorkflowMapper mapper,
        IWorkflowValidator validator)
    {
        _mapper = mapper;
        _validator = validator;
    }

    public async ValueTask ValidateAsync(
        WorkflowPackage package,
        WorkflowPackageValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(context);

        var document = _mapper.ToDocument(package.Workflow);

        var result = await _validator.ValidateAsync(
            document,
            context.CancellationToken);

        foreach (var error in result.Errors)
        {
            context.Add(
                new WorkflowPackageDiagnostic(
                    error.Code,
                    error.Message));
        }
    }
}