using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;

namespace PulseStack.Core.Persistence.AIAssets.Validation;

internal static class WorkflowDocumentStructuralValidator
{
    public static void Validate(
        WorkflowAssetDocument workflow,
        ICollection<AIAssetDocumentValidationError> errors,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workflow);
        ArgumentNullException.ThrowIfNull(errors);

        cancellationToken.ThrowIfCancellationRequested();

        var seenStepIds = new HashSet<Guid>();

        for (var index = 0; index < workflow.Steps.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateStep(
                workflow.Steps[index],
                $"$.steps[{index}]",
                seenStepIds,
                errors,
                cancellationToken);
        }
    }

    private static void ValidateStep(
        WorkflowStepDocument? step,
        string path,
        ISet<Guid> seenStepIds,
        ICollection<AIAssetDocumentValidationError> errors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (step is null)
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.MissingWorkflowStep,
                "Workflow step is required.",
                path);
            return;
        }

        ValidateStepId(step.StepId, $"{path}.stepId", seenStepIds, errors);

        switch (step)
        {
            case RunStepDocument:
                return;

            case ParallelStepDocument parallel:
                for (var index = 0; index < parallel.Steps.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateStep(
                        parallel.Steps[index],
                        $"{path}.steps[{index}]",
                        seenStepIds,
                        errors,
                        cancellationToken);
                }

                return;

            case ConditionalStepDocument conditional:
                ValidateStep(
                    conditional.ThenStep,
                    $"{path}.thenStep",
                    seenStepIds,
                    errors,
                    cancellationToken);

                if (conditional.ElseStep is not null)
                {
                    ValidateStep(
                        conditional.ElseStep,
                        $"{path}.elseStep",
                        seenStepIds,
                        errors,
                        cancellationToken);
                }

                return;

            case RetryStepDocument retry:
                ValidateStep(
                    retry.Step,
                    $"{path}.step",
                    seenStepIds,
                    errors,
                    cancellationToken);
                return;

            case LoopStepDocument loop:
                ValidateStep(
                    loop.Step,
                    $"{path}.step",
                    seenStepIds,
                    errors,
                    cancellationToken);
                return;

            case SwitchStepDocument @switch:
                for (var index = 0; index < @switch.Cases.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var @case = @switch.Cases[index];
                    if (@case is null)
                    {
                        continue;
                    }

                    ValidateStep(
                        @case.Step,
                        $"{path}.cases[{index}].step",
                        seenStepIds,
                        errors,
                        cancellationToken);
                }

                if (@switch.DefaultStep is not null)
                {
                    ValidateStep(
                        @switch.DefaultStep,
                        $"{path}.defaultStep",
                        seenStepIds,
                        errors,
                        cancellationToken);
                }

                return;
        }
    }

    private static void ValidateStepId(
        string? value,
        string path,
        ISet<Guid> seenStepIds,
        ICollection<AIAssetDocumentValidationError> errors)
    {
        if (!Guid.TryParseExact(value, "D", out var parsed)
            || parsed == Guid.Empty
            || !string.Equals(value, parsed.ToString("D"), StringComparison.Ordinal))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.InvalidWorkflowStepId,
                "Workflow step ID must be a non-empty canonical lowercase GUID D value.",
                path);
            return;
        }

        if (!seenStepIds.Add(parsed))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.DuplicateWorkflowStepId,
                "Workflow step ID must be unique across the Workflow.",
                path);
        }
    }

    private static void AddError(
        ICollection<AIAssetDocumentValidationError> errors,
        string code,
        string message,
        string path)
    {
        errors.Add(new AIAssetDocumentValidationError(code, message, path));
    }
}
