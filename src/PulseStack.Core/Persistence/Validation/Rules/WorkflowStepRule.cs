using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Core.Persistence.Validation.Diagnostics;

namespace PulseStack.Core.Persistence.Validation.Rules;

internal static class WorkflowStepRule
{
    public static void Validate(
        WorkflowStepDocument step,
        WorkflowValidationContext context)
    {
        context.ThrowIfCancellationRequested();

        ValidateStepIdentity(step, context);

        ValidateStepDefinition(step, context);

        ValidateChildren(step, context);
    }
    
      private static void ValidateStepIdentity(
        WorkflowStepDocument step,
        WorkflowValidationContext context)
    {
        if (step.Id == default)
        {
            context.AddError(
                WorkflowDiagnosticDescriptors.StepIdMissing);

            return;
        }

        if (!context.VisitedStepIds.Add(step.Id))
        {
            context.AddError(
                WorkflowDiagnosticDescriptors.DuplicateStepId);
        }
    }

    private static void ValidateStepDefinition(
        WorkflowStepDocument step,
        WorkflowValidationContext context)
    {

        if (string.IsNullOrWhiteSpace(step.Name))
        {
            context.AddError(
                WorkflowDiagnosticDescriptors.StepNameMissing);
        }
    }

    private static void ValidateChildren(
        WorkflowStepDocument step,
        WorkflowValidationContext context)
    {
        foreach (var child in step.Children)
        {
            Validate(child, context);
        }
    }
    
}