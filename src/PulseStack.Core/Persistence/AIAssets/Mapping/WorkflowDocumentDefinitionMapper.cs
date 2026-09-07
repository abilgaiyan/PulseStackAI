using System.Collections.ObjectModel;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Values;

namespace PulseStack.Core.Persistence.AIAssets.Mapping;

internal static class WorkflowDocumentDefinitionMapper
{
    internal static WorkflowAssetOptions ToOptions(
        WorkflowAssetDocument document,
        AssetMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(metadata);

        var steps = new WorkflowStepDefinition[document.Steps.Count];
        for (var index = 0; index < document.Steps.Count; index++)
        {
            steps[index] = FromDocument(document.Steps[index], $"$.steps[{index}]");
        }

        return new WorkflowAssetOptions
        {
            Name = Require(metadata.Name, "Workflow name", "$.metadata.name"),
            Description = metadata.Description,
            Steps = steps
        };
    }

    private static WorkflowStepDefinition FromDocument(
        WorkflowStepDocument document,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document switch
        {
            RunStepDocument run => FromRun(run, path),
            ParallelStepDocument parallel => FromParallel(parallel, path),
            ConditionalStepDocument conditional => FromConditional(conditional, path),
            RetryStepDocument retry => FromRetry(retry, path),
            LoopStepDocument loop => FromLoop(loop, path),
            SwitchStepDocument @switch => FromSwitch(@switch, path),
            _ => throw Unsupported(path, "Workflow step", document.GetType())
        };
    }

    private static RunStepDefinition FromRun(RunStepDocument document, string path)
    {
        EnsureKind(document, WorkflowStepDocumentKind.Run, path);
        return new RunStepDefinition
        {
            Id = ParseStepId(document.StepId, $"{path}.stepId"),
            Agent = FromDocument(document.Agent, $"{path}.agent")
        };
    }

    private static ParallelStepDefinition FromParallel(
        ParallelStepDocument document,
        string path)
    {
        EnsureKind(document, WorkflowStepDocumentKind.Parallel, path);
        return new ParallelStepDefinition
        {
            Id = ParseStepId(document.StepId, $"{path}.stepId"),
            Name = document.Name,
            Steps = MapSteps(document.Steps, $"{path}.steps")
        };
    }

    private static ConditionalStepDefinition FromConditional(
        ConditionalStepDocument document,
        string path)
    {
        EnsureKind(document, WorkflowStepDocumentKind.Conditional, path);
        return new ConditionalStepDefinition
        {
            Id = ParseStepId(document.StepId, $"{path}.stepId"),
            Name = document.Name,
            Condition = FromDocument(document.Condition, $"{path}.condition"),
            ThenStep = FromDocument(document.ThenStep, $"{path}.thenStep"),
            ElseStep = document.ElseStep is null
                ? null
                : FromDocument(document.ElseStep, $"{path}.elseStep")
        };
    }

    private static RetryStepDefinition FromRetry(RetryStepDocument document, string path)
    {
        EnsureKind(document, WorkflowStepDocumentKind.Retry, path);
        return new RetryStepDefinition
        {
            Id = ParseStepId(document.StepId, $"{path}.stepId"),
            Name = document.Name,
            Step = FromDocument(document.Step, $"{path}.step"),
            MaxAttempts = document.MaxAttempts
        };
    }

    private static LoopStepDefinition FromLoop(LoopStepDocument document, string path)
    {
        EnsureKind(document, WorkflowStepDocumentKind.Loop, path);
        return new LoopStepDefinition
        {
            Id = ParseStepId(document.StepId, $"{path}.stepId"),
            Name = document.Name,
            Items = FromDocument(document.Items, $"{path}.items"),
            Step = FromDocument(document.Step, $"{path}.step")
        };
    }

    private static SwitchStepDefinition FromSwitch(
        SwitchStepDocument document,
        string path)
    {
        EnsureKind(document, WorkflowStepDocumentKind.Switch, path);
        return new SwitchStepDefinition
        {
            Id = ParseStepId(document.StepId, $"{path}.stepId"),
            Name = document.Name,
            Selector = FromDocument(document.Selector, $"{path}.selector"),
            Cases = MapCases(document.Cases, $"{path}.cases"),
            DefaultStep = document.DefaultStep is null
                ? null
                : FromDocument(document.DefaultStep, $"{path}.defaultStep")
        };
    }

    private static IReadOnlyList<WorkflowStepDefinition> MapSteps(
        IReadOnlyList<WorkflowStepDocument> documents,
        string path)
    {
        var steps = new WorkflowStepDefinition[documents.Count];
        for (var index = 0; index < documents.Count; index++)
        {
            steps[index] = FromDocument(documents[index], $"{path}[{index}]");
        }

        return steps;
    }

    private static IReadOnlyList<SwitchCaseDefinition> MapCases(
        IReadOnlyList<SwitchCaseDocument> documents,
        string path)
    {
        var cases = new SwitchCaseDefinition[documents.Count];
        for (var index = 0; index < documents.Count; index++)
        {
            var document = documents[index];
            cases[index] = new SwitchCaseDefinition
            {
                Value = document.Value,
                Step = FromDocument(document.Step, $"{path}[{index}].step")
            };
        }

        return cases;
    }

    private static ConditionDefinition FromDocument(
        WorkflowConditionDocument document,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document switch
        {
            NamedConditionDocument named => FromNamedCondition(named, path),
            _ => throw Unsupported(path, "Workflow condition", document.GetType())
        };
    }

    private static NamedConditionDefinition FromNamedCondition(
        NamedConditionDocument document,
        string path)
    {
        EnsureKind(document, WorkflowConditionDocumentKind.Named, path);
        return new NamedConditionDefinition
        {
            Name = document.Name
        };
    }

    private static WorkflowValueDefinition FromDocument(
        WorkflowValueDocument document,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document switch
        {
            InputValueDocument input => FromInput(input, path),
            CurrentOutputValueDocument currentOutput => FromCurrentOutput(currentOutput, path),
            ContextItemValueDocument contextItem => FromContextItem(contextItem, path),
            LiteralValueDocument literal => FromLiteralValue(literal, path),
            _ => throw Unsupported(path, "Workflow value", document.GetType())
        };
    }

    private static InputValueDefinition FromInput(InputValueDocument document, string path)
    {
        EnsureKind(document, WorkflowValueDocumentKind.Input, path);
        return new InputValueDefinition();
    }

    private static CurrentOutputValueDefinition FromCurrentOutput(
        CurrentOutputValueDocument document,
        string path)
    {
        EnsureKind(document, WorkflowValueDocumentKind.CurrentOutput, path);
        return new CurrentOutputValueDefinition();
    }

    private static ContextItemValueDefinition FromContextItem(
        ContextItemValueDocument document,
        string path)
    {
        EnsureKind(document, WorkflowValueDocumentKind.ContextItem, path);
        return new ContextItemValueDefinition
        {
            Key = document.Key
        };
    }

    private static LiteralValueDefinition FromLiteralValue(
        LiteralValueDocument document,
        string path)
    {
        EnsureKind(document, WorkflowValueDocumentKind.Literal, path);
        return new LiteralValueDefinition
        {
            Value = FromDocument(document.Literal, $"{path}.literal")
        };
    }

    private static object? FromDocument(
        WorkflowLiteralDocument document,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);

        switch (document)
        {
            case NullWorkflowLiteralDocument literal:
                EnsureKind(literal, WorkflowLiteralDocumentKind.Null, path);
                return null;

            case StringWorkflowLiteralDocument literal:
                EnsureKind(literal, WorkflowLiteralDocumentKind.String, path);
                return literal.Value;

            case BooleanWorkflowLiteralDocument literal:
                EnsureKind(literal, WorkflowLiteralDocumentKind.Boolean, path);
                return literal.Value;

            case IntegerWorkflowLiteralDocument literal:
                EnsureKind(literal, WorkflowLiteralDocumentKind.Integer, path);
                return literal.Value;

            case DecimalWorkflowLiteralDocument literal:
                EnsureKind(literal, WorkflowLiteralDocumentKind.Decimal, path);
                return literal.Value;

            case ArrayWorkflowLiteralDocument literal:
                EnsureKind(literal, WorkflowLiteralDocumentKind.Array, path);
                return ReconstructArray(literal, path);

            case ObjectWorkflowLiteralDocument literal:
                EnsureKind(literal, WorkflowLiteralDocumentKind.Object, path);
                return ReconstructObject(literal, path);

            default:
                throw Unsupported(path, "Workflow literal", document.GetType());
        }
    }

    private static IReadOnlyList<object?> ReconstructArray(
        ArrayWorkflowLiteralDocument document,
        string path)
    {
        var values = new object?[document.Items.Count];
        for (var index = 0; index < document.Items.Count; index++)
        {
            values[index] = FromDocument(document.Items[index], $"{path}.items[{index}]");
        }

        return values;
    }

    private static IReadOnlyDictionary<string, object?> ReconstructObject(
        ObjectWorkflowLiteralDocument document,
        string path)
    {
        var values = new SortedDictionary<string, object?>(StringComparer.Ordinal);
        for (var index = 0; index < document.Properties.Count; index++)
        {
            var property = document.Properties[index];
            values.Add(
                property.Name,
                FromDocument(property.Value, $"{path}.properties[{index}].value"));
        }

        return new ReadOnlyDictionary<string, object?>(values);
    }

    private static AssetReference FromDocument(
        AIAssetReferenceDocument document,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);

        var type = document.AssetType switch
        {
            AIAssetDocumentType.Project => AssetType.Project,
            AIAssetDocumentType.Library => AssetType.Library,
            AIAssetDocumentType.Package => AssetType.Package,
            AIAssetDocumentType.Workflow => AssetType.Workflow,
            AIAssetDocumentType.Agent => AssetType.Agent,
            AIAssetDocumentType.Prompt => AssetType.Prompt,
            AIAssetDocumentType.Tool => AssetType.Tool,
            AIAssetDocumentType.Knowledge => AssetType.Knowledge,
            AIAssetDocumentType.Memory => AssetType.Memory,
            AIAssetDocumentType.Policy => AssetType.Policy,
            AIAssetDocumentType.Provider => AssetType.Provider,
            AIAssetDocumentType.Model => AssetType.Model,
            _ => throw new NotSupportedException(
                $"Workflow Asset reference type '{document.AssetType}' is not supported at '{path}'.")
        };

        if (!Guid.TryParse(document.AssetId, out var id) || id == Guid.Empty)
        {
            throw Invalid($"{path}.assetId", "Workflow Asset reference ID is invalid.");
        }

        return new AssetReference(
            type,
            new AssetId(id),
            new AssetUrn(Require(document.Urn, "Workflow Asset reference URN", $"{path}.urn")),
            new AssetVersion(Require(document.Version, "Workflow Asset reference version", $"{path}.version")));
    }

    private static WorkflowStepId ParseStepId(string value, string path)
    {
        if (!Guid.TryParseExact(value, "D", out var parsed)
            || parsed == Guid.Empty
            || !string.Equals(value, parsed.ToString("D"), StringComparison.Ordinal))
        {
            throw Invalid(path, "Workflow StepId must be a canonical lowercase non-empty GUID in D format.");
        }

        return new WorkflowStepId(parsed);
    }

    private static void EnsureKind(
        WorkflowStepDocument document,
        WorkflowStepDocumentKind expected,
        string path)
    {
        if (document.Kind != expected)
        {
            throw Invalid(path, $"Workflow step discriminator '{document.Kind}' does not match '{expected}'.");
        }
    }

    private static void EnsureKind(
        WorkflowConditionDocument document,
        WorkflowConditionDocumentKind expected,
        string path)
    {
        if (document.Kind != expected)
        {
            throw Invalid(path, $"Workflow condition discriminator '{document.Kind}' does not match '{expected}'.");
        }
    }

    private static void EnsureKind(
        WorkflowValueDocument document,
        WorkflowValueDocumentKind expected,
        string path)
    {
        if (document.Kind != expected)
        {
            throw Invalid(path, $"Workflow value discriminator '{document.Kind}' does not match '{expected}'.");
        }
    }

    private static void EnsureKind(
        WorkflowLiteralDocument document,
        WorkflowLiteralDocumentKind expected,
        string path)
    {
        if (document.Kind != expected)
        {
            throw Invalid(path, $"Workflow literal discriminator '{document.Kind}' does not match '{expected}'.");
        }
    }

    private static string Require(string? value, string field, string path)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(path, $"{field} is required.");
        }

        return value;
    }

    private static InvalidOperationException Invalid(string path, string reason)
        => new($"Workflow document value at '{path}' is invalid. {reason}");

    private static NotSupportedException Unsupported(string path, string category, Type type)
        => new($"{category} type '{type.FullName}' is not supported for reconstruction at '{path}'.");
}
