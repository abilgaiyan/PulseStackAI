using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Core.Persistence.Validation.Diagnostics;

namespace PulseStack.Core.Persistence.Validation.Rules;

internal static class WorkflowIdentityRule
{
    public static void Validate(
        WorkflowDocument document,
        WorkflowValidationContext context)
    {
        context.ThrowIfCancellationRequested();

        if (document.Identity.Id == default)
        {
            context.AddError(
                WorkflowDiagnosticDescriptors.WorkflowIdMissing);
        }

        if (string.IsNullOrWhiteSpace(document.Identity.Version))
        {
            context.AddError(
                WorkflowDiagnosticDescriptors.WorkflowVersionMissing);
        }
    }
}