using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
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
            AddError(errors, AIAssetDocumentValidationCodes.MissingWorkflowStep, "Workflow step is required.", path);
            return;
        }

        ValidateStepId(step.StepId, $"{path}.stepId", seenStepIds, errors);

        if (!TryGetExpectedStepKind(step, out var expectedKind))
        {
            AddError(errors, AIAssetDocumentValidationCodes.UnsupportedWorkflowStep, "Workflow step type is not supported.", path);
            return;
        }

        if (step.Kind != expectedKind)
        {
            AddError(errors, AIAssetDocumentValidationCodes.WorkflowStepTypeMismatch, "Workflow step type does not match its discriminator.", $"{path}.kind");
            return;
        }

        switch (step)
        {
            case RunStepDocument run:
                ValidateRunReference(run.Agent, $"{path}.agent", errors);
                return;

            case ParallelStepDocument parallel:
                ValidateStepName(parallel.Name, $"{path}.name", errors);
                for (var index = 0; index < parallel.Steps.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateStep(parallel.Steps[index], $"{path}.steps[{index}]", seenStepIds, errors, cancellationToken);
                }
                return;

            case ConditionalStepDocument conditional:
                ValidateStepName(conditional.Name, $"{path}.name", errors);
                ValidateCondition(conditional.Condition, $"{path}.condition", errors, cancellationToken);
                ValidateStep(conditional.ThenStep, $"{path}.thenStep", seenStepIds, errors, cancellationToken);
                if (conditional.ElseStep is not null)
                {
                    ValidateStep(conditional.ElseStep, $"{path}.elseStep", seenStepIds, errors, cancellationToken);
                }
                return;

            case RetryStepDocument retry:
                ValidateStepName(retry.Name, $"{path}.name", errors);
                if (retry.MaxAttempts < 1)
                {
                    AddError(errors, AIAssetDocumentValidationCodes.InvalidRetryMaxAttempts, "Retry max attempts must be at least one.", $"{path}.maxAttempts");
                }
                ValidateStep(retry.Step, $"{path}.step", seenStepIds, errors, cancellationToken);
                return;

            case LoopStepDocument loop:
                ValidateStepName(loop.Name, $"{path}.name", errors);
                ValidateValue(loop.Items, $"{path}.items", errors, cancellationToken);
                ValidateStep(loop.Step, $"{path}.step", seenStepIds, errors, cancellationToken);
                return;

            case SwitchStepDocument @switch:
                ValidateStepName(@switch.Name, $"{path}.name", errors);
                ValidateValue(@switch.Selector, $"{path}.selector", errors, cancellationToken);
                var seenCaseValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < @switch.Cases.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var @case = @switch.Cases[index];
                    var casePath = $"{path}.cases[{index}]";
                    if (@case is null)
                    {
                        AddError(errors, AIAssetDocumentValidationCodes.MissingSwitchCase, "Switch case is required.", casePath);
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(@case.Value))
                    {
                        AddError(errors, AIAssetDocumentValidationCodes.InvalidSwitchCaseValue, "Switch case value is required.", $"{casePath}.value");
                    }
                    else if (!seenCaseValues.Add(@case.Value))
                    {
                        AddError(errors, AIAssetDocumentValidationCodes.DuplicateSwitchCaseValue, "Switch case values must be unique using ordinal-ignore-case comparison.", $"{casePath}.value");
                    }
                    ValidateStep(@case.Step, $"{casePath}.step", seenStepIds, errors, cancellationToken);
                }
                if (@switch.DefaultStep is not null)
                {
                    ValidateStep(@switch.DefaultStep, $"{path}.defaultStep", seenStepIds, errors, cancellationToken);
                }
                return;
        }
    }

    private static void ValidateCondition(
        WorkflowConditionDocument? condition,
        string path,
        ICollection<AIAssetDocumentValidationError> errors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (condition is null)
        {
            AddError(errors, AIAssetDocumentValidationCodes.MissingWorkflowCondition, "Workflow condition is required.", path);
            return;
        }
        if (condition is not NamedConditionDocument named)
        {
            AddError(errors, AIAssetDocumentValidationCodes.UnsupportedWorkflowCondition, "Workflow condition type is not supported.", path);
            return;
        }
        if (condition.Kind != WorkflowConditionDocumentKind.Named)
        {
            AddError(errors, AIAssetDocumentValidationCodes.WorkflowConditionTypeMismatch, "Workflow condition type does not match its discriminator.", $"{path}.kind");
            return;
        }
        if (string.IsNullOrWhiteSpace(named.Name))
        {
            AddError(errors, AIAssetDocumentValidationCodes.MissingNamedConditionName, "Named condition name is required.", $"{path}.name");
        }
    }

    private static void ValidateValue(
        WorkflowValueDocument? value,
        string path,
        ICollection<AIAssetDocumentValidationError> errors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null)
        {
            AddError(errors, AIAssetDocumentValidationCodes.MissingWorkflowValue, "Workflow value is required.", path);
            return;
        }
        if (!TryGetExpectedValueKind(value, out var expectedKind))
        {
            AddError(errors, AIAssetDocumentValidationCodes.UnsupportedWorkflowValue, "Workflow value type is not supported.", path);
            return;
        }
        if (value.Kind != expectedKind)
        {
            AddError(errors, AIAssetDocumentValidationCodes.WorkflowValueTypeMismatch, "Workflow value type does not match its discriminator.", $"{path}.kind");
            return;
        }

        switch (value)
        {
            case InputValueDocument:
            case CurrentOutputValueDocument:
                return;
            case ContextItemValueDocument contextItem:
                if (string.IsNullOrWhiteSpace(contextItem.Key))
                {
                    AddError(errors, AIAssetDocumentValidationCodes.MissingContextItemKey, "Context-item key is required.", $"{path}.key");
                }
                return;
            case LiteralValueDocument literalValue:
                if (literalValue.Literal is null)
                {
                    AddError(errors, AIAssetDocumentValidationCodes.MissingWorkflowLiteral, "Workflow literal is required.", $"{path}.literal");
                    return;
                }
                ValidateLiteral(literalValue.Literal, $"{path}.literal", errors, cancellationToken);
                return;
        }
    }

    private static void ValidateLiteral(
        WorkflowLiteralDocument literal,
        string path,
        ICollection<AIAssetDocumentValidationError> errors,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TryGetExpectedLiteralKind(literal, out var expectedKind))
        {
            AddError(errors, AIAssetDocumentValidationCodes.UnsupportedWorkflowLiteral, "Workflow literal type is not supported.", path);
            return;
        }

        if (literal.Kind != expectedKind)
        {
            AddError(errors, AIAssetDocumentValidationCodes.WorkflowLiteralTypeMismatch, "Workflow literal type does not match its discriminator.", $"{path}.kind");
            return;
        }

        switch (literal)
        {
            case NullWorkflowLiteralDocument:
            case BooleanWorkflowLiteralDocument:
            case IntegerWorkflowLiteralDocument:
            case DecimalWorkflowLiteralDocument:
                return;

            case StringWorkflowLiteralDocument @string:
                if (@string.Value is null)
                {
                    AddError(errors, AIAssetDocumentValidationCodes.MissingWorkflowStringLiteralValue, "Workflow string literal value is required.", $"{path}.value");
                }
                return;

            case ArrayWorkflowLiteralDocument array:
                for (var index = 0; index < array.Items.Count; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var itemPath = $"{path}.items[{index}]";
                    var item = array.Items[index];
                    if (item is null)
                    {
                        AddError(errors, AIAssetDocumentValidationCodes.MissingWorkflowArrayItem, "Workflow literal array item is required.", itemPath);
                        continue;
                    }
                    ValidateLiteral(item, itemPath, errors, cancellationToken);
                }
                return;

            case ObjectWorkflowLiteralDocument @object:
                ValidateObjectLiteral(@object, path, errors, cancellationToken);
                return;
        }
    }

    private static void ValidateObjectLiteral(
        ObjectWorkflowLiteralDocument @object,
        string path,
        ICollection<AIAssetDocumentValidationError> errors,
        CancellationToken cancellationToken)
    {
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        string? previousValidName = null;

        for (var index = 0; index < @object.Properties.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var property = @object.Properties[index];
            var propertyPath = $"{path}.properties[{index}]";

            if (property is null)
            {
                AddError(errors, AIAssetDocumentValidationCodes.MissingWorkflowObjectProperty, "Workflow literal object property is required.", propertyPath);
                continue;
            }

            var hasValue = property.Value is not null;
            if (!hasValue)
            {
                AddError(errors, AIAssetDocumentValidationCodes.MissingWorkflowObjectPropertyValue, "Workflow literal object property value is required.", $"{propertyPath}.value");
            }

            var hasValidName = !string.IsNullOrWhiteSpace(property.Name);
            if (!hasValidName)
            {
                AddError(errors, AIAssetDocumentValidationCodes.InvalidWorkflowObjectPropertyName, "Workflow literal object property name is required.", $"{propertyPath}.name");
            }
            else
            {
                if (!seenNames.Add(property.Name))
                {
                    AddError(errors, AIAssetDocumentValidationCodes.DuplicateWorkflowObjectPropertyName, "Workflow literal object property names must be unique using ordinal comparison.", $"{propertyPath}.name");
                }

                if (previousValidName is not null
                    && StringComparer.Ordinal.Compare(previousValidName, property.Name) > 0)
                {
                    AddError(errors, AIAssetDocumentValidationCodes.NonCanonicalWorkflowObjectPropertyOrder, "Workflow literal object properties must be ordered by name using ordinal comparison.", $"{propertyPath}.name");
                }

                previousValidName = property.Name;
            }

            if (hasValue)
            {
                ValidateLiteral(property.Value!, $"{propertyPath}.value", errors, cancellationToken);
            }
        }
    }

    private static void ValidateRunReference(
        AIAssetReferenceDocument? reference,
        string path,
        ICollection<AIAssetDocumentValidationError> errors)
    {
        if (reference is null)
        {
            AddError(errors, AIAssetDocumentValidationCodes.MissingRunAgentReference, "Run step Agent reference is required.", path);
            return;
        }

        var isStructurallyValid = Enum.IsDefined(reference.AssetType)
            && Guid.TryParse(reference.AssetId, out var assetId)
            && assetId != Guid.Empty
            && !string.IsNullOrWhiteSpace(reference.Urn)
            && !string.IsNullOrWhiteSpace(reference.Version);

        if (!isStructurallyValid)
        {
            AddError(errors, AIAssetDocumentValidationCodes.InvalidRunAgentReference, "Run step Agent reference is invalid.", path);
            return;
        }

        if (reference.AssetType != AIAssetDocumentType.Agent)
        {
            AddError(errors, AIAssetDocumentValidationCodes.InvalidRunAgentReferenceType, "Run step reference must target an Agent asset.", path);
        }
    }

    private static void ValidateStepName(string? name, string path, ICollection<AIAssetDocumentValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            AddError(errors, AIAssetDocumentValidationCodes.MissingWorkflowStepName, "Workflow step name is required.", path);
        }
    }

    private static bool TryGetExpectedStepKind(WorkflowStepDocument step, out WorkflowStepDocumentKind kind)
    {
        switch (step)
        {
            case RunStepDocument: kind = WorkflowStepDocumentKind.Run; return true;
            case ParallelStepDocument: kind = WorkflowStepDocumentKind.Parallel; return true;
            case ConditionalStepDocument: kind = WorkflowStepDocumentKind.Conditional; return true;
            case RetryStepDocument: kind = WorkflowStepDocumentKind.Retry; return true;
            case LoopStepDocument: kind = WorkflowStepDocumentKind.Loop; return true;
            case SwitchStepDocument: kind = WorkflowStepDocumentKind.Switch; return true;
            default: kind = default; return false;
        }
    }

    private static bool TryGetExpectedValueKind(WorkflowValueDocument value, out WorkflowValueDocumentKind kind)
    {
        switch (value)
        {
            case InputValueDocument: kind = WorkflowValueDocumentKind.Input; return true;
            case CurrentOutputValueDocument: kind = WorkflowValueDocumentKind.CurrentOutput; return true;
            case ContextItemValueDocument: kind = WorkflowValueDocumentKind.ContextItem; return true;
            case LiteralValueDocument: kind = WorkflowValueDocumentKind.Literal; return true;
            default: kind = default; return false;
        }
    }

    private static bool TryGetExpectedLiteralKind(WorkflowLiteralDocument literal, out WorkflowLiteralDocumentKind kind)
    {
        switch (literal)
        {
            case NullWorkflowLiteralDocument: kind = WorkflowLiteralDocumentKind.Null; return true;
            case StringWorkflowLiteralDocument: kind = WorkflowLiteralDocumentKind.String; return true;
            case BooleanWorkflowLiteralDocument: kind = WorkflowLiteralDocumentKind.Boolean; return true;
            case IntegerWorkflowLiteralDocument: kind = WorkflowLiteralDocumentKind.Integer; return true;
            case DecimalWorkflowLiteralDocument: kind = WorkflowLiteralDocumentKind.Decimal; return true;
            case ArrayWorkflowLiteralDocument: kind = WorkflowLiteralDocumentKind.Array; return true;
            case ObjectWorkflowLiteralDocument: kind = WorkflowLiteralDocumentKind.Object; return true;
            default: kind = default; return false;
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
            AddError(errors, AIAssetDocumentValidationCodes.InvalidWorkflowStepId, "Workflow step ID must be a non-empty canonical lowercase GUID D value.", path);
            return;
        }
        if (!seenStepIds.Add(parsed))
        {
            AddError(errors, AIAssetDocumentValidationCodes.DuplicateWorkflowStepId, "Workflow step ID must be unique across the Workflow.", path);
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
