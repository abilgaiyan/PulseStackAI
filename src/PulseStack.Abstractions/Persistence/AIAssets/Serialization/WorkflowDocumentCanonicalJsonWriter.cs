using System.Globalization;
using System.Text.Json;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Serialization;

internal static class WorkflowDocumentCanonicalJsonWriter
{
    internal static void WriteSteps(Utf8JsonWriter writer, IReadOnlyList<WorkflowStepDocument> steps)
    {
        writer.WritePropertyName("steps");
        writer.WriteStartArray();
        foreach (var step in steps) WriteStep(writer, step);
        writer.WriteEndArray();
    }

    private static void WriteStep(Utf8JsonWriter writer, WorkflowStepDocument step)
    {
        var expected = step switch
        {
            RunStepDocument => WorkflowStepDocumentKind.Run,
            ParallelStepDocument => WorkflowStepDocumentKind.Parallel,
            ConditionalStepDocument => WorkflowStepDocumentKind.Conditional,
            RetryStepDocument => WorkflowStepDocumentKind.Retry,
            LoopStepDocument => WorkflowStepDocumentKind.Loop,
            SwitchStepDocument => WorkflowStepDocumentKind.Switch,
            _ => throw Unrepresentable("kind", step.GetType().Name)
        };
        EnsureKind(step.Kind, expected);

        writer.WriteStartObject();
        switch (step)
        {
            case RunStepDocument run:
                writer.WritePropertyName("agent"); WriteReference(writer, run.Agent);
                writer.WriteString("kind", "run");
                WriteString(writer, "stepId", run.StepId);
                break;
            case ParallelStepDocument parallel:
                writer.WriteString("kind", "parallel");
                WriteString(writer, "name", parallel.Name);
                WriteString(writer, "stepId", parallel.StepId);
                writer.WritePropertyName("steps"); writer.WriteStartArray();
                foreach (var child in parallel.Steps) WriteStep(writer, child);
                writer.WriteEndArray();
                break;
            case ConditionalStepDocument conditional:
                writer.WritePropertyName("condition"); WriteCondition(writer, conditional.Condition);
                writer.WritePropertyName("elseStep");
                if (conditional.ElseStep is null) writer.WriteNullValue(); else WriteStep(writer, conditional.ElseStep);
                writer.WriteString("kind", "conditional");
                WriteString(writer, "name", conditional.Name);
                WriteString(writer, "stepId", conditional.StepId);
                writer.WritePropertyName("thenStep"); WriteStep(writer, conditional.ThenStep);
                break;
            case RetryStepDocument retry:
                writer.WriteString("kind", "retry");
                writer.WriteNumber("maxAttempts", retry.MaxAttempts);
                WriteString(writer, "name", retry.Name);
                writer.WritePropertyName("step"); WriteStep(writer, retry.Step);
                WriteString(writer, "stepId", retry.StepId);
                break;
            case LoopStepDocument loop:
                writer.WritePropertyName("items"); WriteValue(writer, loop.Items);
                writer.WriteString("kind", "loop");
                WriteString(writer, "name", loop.Name);
                writer.WritePropertyName("step"); WriteStep(writer, loop.Step);
                WriteString(writer, "stepId", loop.StepId);
                break;
            case SwitchStepDocument sw:
                writer.WritePropertyName("cases"); writer.WriteStartArray();
                foreach (var item in sw.Cases)
                {
                    writer.WriteStartObject();
                    writer.WritePropertyName("step"); WriteStep(writer, item.Step);
                    WriteString(writer, "value", item.Value);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WritePropertyName("defaultStep");
                if (sw.DefaultStep is null) writer.WriteNullValue(); else WriteStep(writer, sw.DefaultStep);
                writer.WriteString("kind", "switch");
                WriteString(writer, "name", sw.Name);
                writer.WritePropertyName("selector"); WriteValue(writer, sw.Selector);
                WriteString(writer, "stepId", sw.StepId);
                break;
        }
        writer.WriteEndObject();
    }

    private static void WriteCondition(Utf8JsonWriter writer, WorkflowConditionDocument condition)
    {
        if (condition is not NamedConditionDocument named || condition.Kind != WorkflowConditionDocumentKind.Named)
            throw Unrepresentable("kind", condition.Kind.ToString());
        writer.WriteStartObject();
        writer.WriteString("kind", "named");
        WriteString(writer, "name", named.Name);
        writer.WriteEndObject();
    }

    private static void WriteValue(Utf8JsonWriter writer, WorkflowValueDocument value)
    {
        writer.WriteStartObject();
        switch (value)
        {
            case InputValueDocument when value.Kind == WorkflowValueDocumentKind.Input:
                writer.WriteString("kind", "input"); break;
            case CurrentOutputValueDocument when value.Kind == WorkflowValueDocumentKind.CurrentOutput:
                writer.WriteString("kind", "currentOutput"); break;
            case ContextItemValueDocument context when value.Kind == WorkflowValueDocumentKind.ContextItem:
                WriteString(writer, "key", context.Key); writer.WriteString("kind", "contextItem"); break;
            case LiteralValueDocument literal when value.Kind == WorkflowValueDocumentKind.Literal:
                writer.WriteString("kind", "literal"); writer.WritePropertyName("literal"); WriteLiteral(writer, literal.Literal); break;
            default: throw Unrepresentable("kind", value.Kind.ToString());
        }
        writer.WriteEndObject();
    }

    private static void WriteLiteral(Utf8JsonWriter writer, WorkflowLiteralDocument literal)
    {
        writer.WriteStartObject();
        switch (literal)
        {
            case NullWorkflowLiteralDocument when literal.Kind == WorkflowLiteralDocumentKind.Null:
                writer.WriteString("kind", "null"); break;
            case StringWorkflowLiteralDocument text when literal.Kind == WorkflowLiteralDocumentKind.String:
                writer.WriteString("kind", "string"); WriteString(writer, "value", text.Value); break;
            case BooleanWorkflowLiteralDocument boolean when literal.Kind == WorkflowLiteralDocumentKind.Boolean:
                writer.WriteString("kind", "boolean"); writer.WriteBoolean("value", boolean.Value); break;
            case IntegerWorkflowLiteralDocument integer when literal.Kind == WorkflowLiteralDocumentKind.Integer:
                writer.WriteString("kind", "integer"); writer.WriteNumber("value", integer.Value); break;
            case DecimalWorkflowLiteralDocument number when literal.Kind == WorkflowLiteralDocumentKind.Decimal:
                writer.WriteString("kind", "decimal"); writer.WritePropertyName("value"); WriteDecimal(writer, number.Value); break;
            case ArrayWorkflowLiteralDocument array when literal.Kind == WorkflowLiteralDocumentKind.Array:
                writer.WritePropertyName("items"); writer.WriteStartArray();
                foreach (var item in array.Items) WriteLiteral(writer, item);
                writer.WriteEndArray(); writer.WriteString("kind", "array"); break;
            case ObjectWorkflowLiteralDocument obj when literal.Kind == WorkflowLiteralDocumentKind.Object:
                writer.WriteString("kind", "object"); writer.WritePropertyName("properties"); writer.WriteStartArray();
                foreach (var property in obj.Properties)
                {
                    writer.WriteStartObject(); WriteString(writer, "name", property.Name);
                    writer.WritePropertyName("value"); WriteLiteral(writer, property.Value); writer.WriteEndObject();
                }
                writer.WriteEndArray(); break;
            default: throw Unrepresentable("kind", literal.Kind.ToString());
        }
        writer.WriteEndObject();
    }

    private static void WriteDecimal(Utf8JsonWriter writer, decimal value)
    {
        if (value == 0m) { writer.WriteRawValue("0", skipInputValidation: false); return; }
        var text = value.ToString("0.#############################", CultureInfo.InvariantCulture);
        writer.WriteRawValue(text, skipInputValidation: false);
    }

    private static void WriteReference(Utf8JsonWriter writer, AIAssetReferenceDocument reference)
    {
        writer.WriteStartObject();
        WriteString(writer, "assetId", reference.AssetId);
        writer.WriteString("assetType", ReferenceToken(reference.AssetType));
        WriteString(writer, "urn", reference.Urn);
        WriteString(writer, "version", reference.Version);
        writer.WriteEndObject();
    }

    private static string ReferenceToken(AIAssetDocumentType type) => type switch
    {
        AIAssetDocumentType.Project => "project", AIAssetDocumentType.Library => "library",
        AIAssetDocumentType.Package => "package", AIAssetDocumentType.Workflow => "workflow",
        AIAssetDocumentType.Agent => "agent", AIAssetDocumentType.Prompt => "prompt",
        AIAssetDocumentType.Tool => "tool", AIAssetDocumentType.Knowledge => "knowledge",
        AIAssetDocumentType.Memory => "memory", AIAssetDocumentType.Policy => "policy",
        AIAssetDocumentType.Provider => "provider", AIAssetDocumentType.Model => "model",
        _ => throw Unrepresentable("assetType", type.ToString())
    };

    private static void WriteString(Utf8JsonWriter writer, string name, string? value)
    {
        writer.WritePropertyName(name);
        if (value is null) { writer.WriteNullValue(); return; }
        EnsureUnicode(value, name); writer.WriteStringValue(value);
    }

    private static void EnsureUnicode(string value, string name)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (char.IsHighSurrogate(value[i])) { if (++i >= value.Length || !char.IsLowSurrogate(value[i])) throw InvalidUnicode(name); }
            else if (char.IsLowSurrogate(value[i])) throw InvalidUnicode(name);
        }
    }

    private static void EnsureKind<T>(T actual, T expected) where T : struct, Enum
    { if (!EqualityComparer<T>.Default.Equals(actual, expected)) throw Unrepresentable("kind", actual.ToString()); }

    private static AIAssetDocumentCodecException InvalidUnicode(string member) => new(
        AIAssetDocumentCodecOperation.Serialization, AIAssetDocumentCodecFailureReason.InvalidUnicode,
        $"Member '{member}' contains invalid Unicode scalar representation.", member);

    private static AIAssetDocumentCodecException Unrepresentable(string member, string token) => new(
        AIAssetDocumentCodecOperation.Serialization, AIAssetDocumentCodecFailureReason.UnrepresentableDocument,
        $"Member '{member}' cannot be represented by the schema-v1 canonical serialization profile.", member, token);
}
