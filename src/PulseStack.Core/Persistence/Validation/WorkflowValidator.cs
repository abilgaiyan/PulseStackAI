using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Abstractions.Persistence.Validation;

namespace PulseStack.Core.Persistence.Validation;

internal sealed class WorkflowValidator : IWorkflowValidator
{
    public ValueTask<WorkflowValidationResult> ValidateAsync(
        WorkflowDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var errors = new List<WorkflowValidationError>();

        ValidateIdentity(document, errors);
        ValidateDefinition(document, errors);
        ValidateStructure(document, errors);

        return ValueTask.FromResult(
            errors.Count == 0
                ? WorkflowValidationResult.Success()
                : WorkflowValidationResult.Failure(errors.ToArray()));
    }

    private static void ValidateIdentity(
        WorkflowDocument document,
        ICollection<WorkflowValidationError> errors)
    {
        if (document.Identity.Id == default)
        {
            AddError(
                errors,
                WorkflowDiagnosticDescriptors.WorkflowIdMissing);
        }

        if (string.IsNullOrWhiteSpace(document.Identity.Version))
        {
            AddError(
                errors,
                WorkflowDiagnosticDescriptors.WorkflowVersionMissing);
        }
    }

    private static void ValidateDefinition(
        WorkflowDocument document,
        ICollection<WorkflowValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(document.Definition.Name))
        {
            AddError(
                errors,
                WorkflowDiagnosticDescriptors.WorkflowNameMissing);
        }
    }

    private static void ValidateStructure(
        WorkflowDocument document,
        ICollection<WorkflowValidationError> errors)
    {
        if (document.Steps.Count == 0)
        {
            AddError(
                errors,
                WorkflowDiagnosticDescriptors.EmptyWorkflow);
        }
    }

    private static void AddError(
        ICollection<WorkflowValidationError> errors,
        WorkflowDiagnosticDescriptor descriptor)
    {
        errors.Add(descriptor.Create());
    }
}