using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Core.Persistence.Validation.Diagnostics;

namespace PulseStack.Core.Persistence.Validation.Rules;
internal static class WorkflowStructureRule
{
    public static void Validate(
        WorkflowDocument document,
        WorkflowValidationContext context)
    {
        context.ThrowIfCancellationRequested();

        if (document.Steps.Count == 0)
        {
            context.AddError(
                WorkflowDiagnosticDescriptors.EmptyWorkflow);

            return;
        }

        foreach (var step in document.Steps)
        {
            WorkflowStepRule.Validate(step, context);
        }
    }
}