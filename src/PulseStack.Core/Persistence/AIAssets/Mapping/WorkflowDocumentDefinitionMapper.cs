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
        var id = ParseStepId(document.StepId, $"{path}.stepId");

        return document switch
        {
            RunStepDocument run => EnsureKind(
                run,
                WorkflowStepDocumentKind.Run,
                path,
                new RunStepDefinition
                {
                    Id = id,
                    Agent = FromDocument(run.Agent, $"{path}.agent")
                }),

            ParallelStepDocument parallel => EnsureKind(
                parallel,
                WorkflowStepDocumentKind.Parallel,
                path,
                new ParallelStepDefinition
                {
                    Id = id,
                    Name = parallel.Name,
                    Steps = MapSteps(parallel.Steps, $"{path}.steps")
                }),

            ConditionalStepDocument conditional => EnsureKind(
                conditional,
                WorkflowStepDocumentKind.Conditional,
                path,
                new ConditionalStepDefinition
                {
                    Id = id,
                    Name = conditional.Name,
                    Condition = FromDocument(conditional.Condition, $"{path}.condition"),
                    ThenStep = FromDocument(conditional.ThenStep, $"{path}.thenStep"),
                    ElseStep = conditional.ElseStep is null
                        ? null
                        : FromDocument(conditional.ElseStep, $"{path}.elseStep")
                }),

            RetryStepDocument retry => EnsureKind(
                retry,
                WorkflowStepDocumentKind.Retry,
                path,
                new RetryStepDefinition
                {
                    Id = id,
                    Name = retry.Name,
                    Step = FromDocument(retry.Step, $"{path}.step"),
                    MaxAttempts = retry.MaxAttempts
                }),

            LoopStepDocument loop => EnsureKind(
                loop,
                WorkflowStepDocumentKind.Loop,
                path,
                new LoopStepDefinition
                {
                    Id = id,
                    Name = loop.Name,
                    Items = FromDocument(loop.Items, $"{path}.items"),
                    Step = FromDocument(loop.Step, $"{path}.step")
                }),

            SwitchStepDocument @switch => EnsureKind(
                @switch,
                WorkflowStepDocumentKind.Switch,
                path,
                new SwitchStepDefinition
                {
                    Id = id,
                    Name = @switch.Name,
                    Selector = FromDocument(@switch.Selector, $"{path}.selector"),
                    Cases = MapCases(@switch.Cases, $"{path}.cases"),
                    DefaultStep = @switch.DefaultStep is null
                        ? null
                        : FromDocument(@switch.DefaultStep, $"{path}.defaultStep")
                }),

            _ => throw Unsupported(path, "Workflow step", document.GetType())
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
            var document = documents[index]
                ?? throw Invalid($"{path}[{index}]", "Switch case is required.");
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
            NamedConditionDocument named => EnsureKind(
                named,
                WorkflowConditionDocumentKind.Named,
                path,
                new NamedConditionDefinition
                {
                    Name = named.Name
                }),
            _ => throw Unsupported(path, "Workflow condition", document.GetType())
        };
    }

    private static WorkflowValueDefinition FromDocument(
        WorkflowValueDocument document,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document switch
        {
            InputValueDocument input => EnsureKind(
                input,
                WorkflowValueDocumentKind.Input,
                path,
                new InputValueDefinition()),

            CurrentOutputValueDocument currentOutput => EnsureKind(
                currentOutput,
                WorkflowValueDocumentKind.CurrentOutput,
                path,
                new CurrentOutputValueDefinition()),

            ContextItemValueDocument contextItem => EnsureKind(
                contextItem,
                WorkflowValueDocumentKind.ContextItem,
                path,
                new ContextItemValueDefinition
                {
                    Key = contextItem.Key
                }),

            LiteralValueDocument literal => EnsureKind(
                literal,
                WorkflowValueDocumentKind.Literal,
                path,
                new LiteralValueDefinition
                {
                    Value = FromDocument(literal.Literal, $"{path}.literal")
                }),

            _ => throw Unsupported(path, "Workflow value", document.GetType())
        };
    }

    private static object? FromDocument(
        WorkflowLiteralDocument document,
        string path)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document switch
        {
            NullWorkflowLiteralDocument literal => EnsureKind<object?>(
                literal,
                WorkflowLiteralDocumentKind.Null,
                path,
                null),

            StringWorkflowLiteralDocument literal => EnsureKind(
                literal,
                WorkflowLiteralDocumentKind.String,
                path,
                literal.Value),

            BooleanWorkflowLiteralDocument literal => EnsureKind(
                literal,
                WorkflowLiteralDocumentKind.Boolean,
                path,
                literal.Value),

            IntegerWorkflowLiteralDocument literal => EnsureKind(
                literal,
                WorkflowLiteralDocumentKind.Integer,
                path,
                literal.Value),

            DecimalWorkflowLiteralDocument literal => EnsureKind(
                literal,
                WorkflowLiteralDocumentKind.Decimal,
                path,
                literal.Value),

            ArrayWorkflowLiteralDocument literal => EnsureKind<object?>(
                literal,
                WorkflowLiteralDocumentKind.Array,
                path,
                ReconstructArray(literal, path)),

            ObjectWorkflowLiteralDocument literal => EnsureKind<object?>(
                literal,
                WorkflowLiteralDocumentKind.Object,
                path,
                ReconstructObject(literal, path)),

            _ => throw Unsupported(path, "Workflow literal", document.GetType())
        };
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
            var property = document.Properties[index]
                ?? throw Invalid($"{path}.properties[{index}]", "Workflow object property is required.");
            var name = Require(
                property.Name,
                "Workflow object property name",
                $"{path}.properties[{index}].name");

            if (!values.TryAdd(
                    name,
                    FromDocument(
                        property.Value,
                        $"{path}.properties[{index}].value")))
            {
                throw Invalid(
                    $"{path}.properties[{index}].name",
                    $"Workflow object property '{name}' is duplicated under ordinal comparison.");
            }
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

    private static T EnsureKind<T>(
        WorkflowStepDocument document,
        WorkflowStepDocumentKind expected,
        string path,
        T value)
    {
        if (document.Kind != expected)
        {
            throw Invalid(path, $"Workflow step discriminator '{document.Kind}' does not match '{expected}'.");
        }

        return value;
    }

    private static T EnsureKind<T>(
        WorkflowConditionDocument document,
        WorkflowConditionDocumentKind expected,
        string path,
        T value)
    {
        if (document.Kind != expected)
        {
            throw Invalid(path, $"Workflow condition discriminator '{document.Kind}' does not match '{expected}'.");
        }

        return value;
    }

    private static T EnsureKind<T>(
        WorkflowValueDocument document,
        WorkflowValueDocumentKind expected,
        string path,
        T value)
    {
        if (document.Kind != expected)
        {
            throw Invalid(path, $"Workflow value discriminator '{document.Kind}' does not match '{expected}'.");
        }

        return value;
    }

    private static T EnsureKind<T>(
        WorkflowLiteralDocument document,
        WorkflowLiteralDocumentKind expected,
        string path,
        T value)
    {
        if (document.Kind != expected)
        {
            throw Invalid(path, $"Workflow literal discriminator '{document.Kind}' does not match '{expected}'.");
        }

        return value;
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
