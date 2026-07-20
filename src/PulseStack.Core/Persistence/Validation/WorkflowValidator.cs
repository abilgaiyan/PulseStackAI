using PulseStack.Abstractions.Persistence.Documents;
using PulseStack.Abstractions.Persistence.Validation;
using PulseStack.Core.Persistence.Validation.Diagnostics;

namespace PulseStack.Core.Persistence.Validation;

public sealed class WorkflowValidator : IWorkflowValidator
{
    public ValueTask<WorkflowValidationResult> ValidateAsync(
        WorkflowDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var context = new WorkflowValidationContext(cancellationToken);

        ValidateIdentity(document, context);
        ValidateStructure(document, context);

        return ValueTask.FromResult(context.CreateResult());
    }

    private static void ValidateIdentity(
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

    private static void ValidateStructure(
        WorkflowDocument document,
        WorkflowValidationContext context)
    {
        context.ThrowIfCancellationRequested();

        if (document.Steps.Count == 0)
        {
            context.AddError(
                WorkflowDiagnosticDescriptors.EmptyWorkflow);
        }

        foreach (var step in document.Steps)
        {
            ValidateStep(step, context);
        }
    }

    private static void ValidateStep(
        WorkflowStepDocument step,
        WorkflowValidationContext context)
    {
        context.ThrowIfCancellationRequested();

        ValidateStepIdentity(step, context);

        ValidateStepDefinition(step, context);

        foreach (var child in step.Children)
        {
            ValidateStep(child, context);
        }
    }

    private static void ValidateStepIdentity(
        WorkflowStepDocument step,
        WorkflowValidationContext context)
    {
        context.ThrowIfCancellationRequested();

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
        context.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(step.Name))
        {
            context.AddError(
                WorkflowDiagnosticDescriptors.StepNameMissing);
        }
    }
}