using System.Text.Json;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;

namespace PulseStack.Abstractions.Persistence.AIAssets.Serialization;

internal static class WorkflowDocumentJsonReader
{
    internal static IReadOnlyList<WorkflowStepDocument> ReadSteps(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'steps' must be an array.", "steps");
        return element.EnumerateArray().Select(ReadStep).ToArray();
    }

    private static WorkflowStepDocument ReadStep(JsonElement element)
    {
        EnsureObjectAndDiscriminator(element, "step");
        var kind = ReadKind(element);
        return kind switch
        {
            "run" => ReadRun(element), "parallel" => ReadParallel(element), "conditional" => ReadConditional(element),
            "retry" => ReadRetry(element), "loop" => ReadLoop(element), "switch" => ReadSwitch(element),
            _ => throw Unknown("workflow step", kind)
        };
    }

    private static RunStepDocument ReadRun(JsonElement e)
    {
        Validate(e, ["agent", "kind", "stepId"]);
        return new RunStepDocument(String(e, "stepId"), AIAssetDocumentJsonReader.ReadReference(AIAssetDocumentJsonReader.Required(e, "agent")));
    }

    private static ParallelStepDocument ReadParallel(JsonElement e)
    {
        Validate(e, ["kind", "name", "steps", "stepId"]);
        return new ParallelStepDocument(String(e, "stepId"), String(e, "name"), ReadSteps(AIAssetDocumentJsonReader.Required(e, "steps")));
    }

    private static ConditionalStepDocument ReadConditional(JsonElement e)
    {
        Validate(e, ["condition", "elseStep", "kind", "name", "stepId", "thenStep"]);
        var otherwise = AIAssetDocumentJsonReader.Required(e, "elseStep");
        return new ConditionalStepDocument(String(e, "stepId"), String(e, "name"),
            ReadCondition(AIAssetDocumentJsonReader.Required(e, "condition")),
            ReadStep(AIAssetDocumentJsonReader.Required(e, "thenStep")),
            otherwise.ValueKind == JsonValueKind.Null ? null : ReadStep(otherwise));
    }

    private static RetryStepDocument ReadRetry(JsonElement e)
    {
        Validate(e, ["kind", "maxAttempts", "name", "step", "stepId"]);
        var attempts = AIAssetDocumentJsonReader.Required(e, "maxAttempts");
        if (attempts.ValueKind != JsonValueKind.Number)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind,
                "Member 'maxAttempts' must be a JSON number.", "maxAttempts");
        if (!attempts.TryGetInt32(out var value))
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidScalarValue,
                "Member 'maxAttempts' is outside Int32 range or is not an integer.", "maxAttempts");
        return new RetryStepDocument(String(e, "stepId"), String(e, "name"), ReadStep(AIAssetDocumentJsonReader.Required(e, "step")), value);
    }

    private static LoopStepDocument ReadLoop(JsonElement e)
    {
        Validate(e, ["items", "kind", "name", "step", "stepId"]);
        return new LoopStepDocument(String(e, "stepId"), String(e, "name"),
            ReadValue(AIAssetDocumentJsonReader.Required(e, "items")), ReadStep(AIAssetDocumentJsonReader.Required(e, "step")));
    }

    private static SwitchStepDocument ReadSwitch(JsonElement e)
    {
        Validate(e, ["cases", "defaultStep", "kind", "name", "selector", "stepId"]);
        var casesElement = AIAssetDocumentJsonReader.Required(e, "cases");
        if (casesElement.ValueKind != JsonValueKind.Array)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'cases' must be an array.", "cases");
        var defaultStep = AIAssetDocumentJsonReader.Required(e, "defaultStep");
        return new SwitchStepDocument(String(e, "stepId"), String(e, "name"),
            ReadValue(AIAssetDocumentJsonReader.Required(e, "selector")), casesElement.EnumerateArray().Select(ReadCase).ToArray(),
            defaultStep.ValueKind == JsonValueKind.Null ? null : ReadStep(defaultStep));
    }

    private static SwitchCaseDocument ReadCase(JsonElement e)
    {
        Validate(e, ["step", "value"]);
        return new SwitchCaseDocument(String(e, "value"), ReadStep(AIAssetDocumentJsonReader.Required(e, "step")));
    }

    private static WorkflowConditionDocument ReadCondition(JsonElement e)
    {
        EnsureObjectAndDiscriminator(e, "condition");
        var kind = ReadKind(e);
        if (!StringComparer.Ordinal.Equals(kind, "named")) throw Unknown("workflow condition", kind);
        Validate(e, ["kind", "name"]);
        return new NamedConditionDocument(String(e, "name"));
    }

    private static WorkflowValueDocument ReadValue(JsonElement e)
    {
        EnsureObjectAndDiscriminator(e, "value");
        var kind = ReadKind(e);
        return kind switch
        {
            "input" => ExactValue(e, ["kind"], () => new InputValueDocument()),
            "currentOutput" => ExactValue(e, ["kind"], () => new CurrentOutputValueDocument()),
            "contextItem" => ExactValue(e, ["key", "kind"], () => new ContextItemValueDocument(String(e, "key"))),
            "literal" => ExactValue(e, ["kind", "literal"], () => new LiteralValueDocument(ReadLiteral(AIAssetDocumentJsonReader.Required(e, "literal")))),
            _ => throw Unknown("workflow value", kind)
        };
    }

    private static T ExactValue<T>(JsonElement e, string[] members, Func<T> create) where T : WorkflowValueDocument
    { Validate(e, members); return create(); }

    private static WorkflowLiteralDocument ReadLiteral(JsonElement e)
    {
        EnsureObjectAndDiscriminator(e, "literal");
        var kind = ReadKind(e);
        return kind switch
        {
            "null" => ExactLiteral(e, ["kind"], () => new NullWorkflowLiteralDocument()),
            "string" => ExactLiteral(e, ["kind", "value"], () => new StringWorkflowLiteralDocument(String(e, "value"))),
            "boolean" => ExactLiteral(e, ["kind", "value"], () => new BooleanWorkflowLiteralDocument(Boolean(e, "value"))),
            "integer" => ExactLiteral(e, ["kind", "value"], () => new IntegerWorkflowLiteralDocument(Int64(e, "value"))),
            "decimal" => ExactLiteral(e, ["kind", "value"], () => new DecimalWorkflowLiteralDocument(Decimal(e, "value"))),
            "array" => ExactLiteral(e, ["items", "kind"], () => new ArrayWorkflowLiteralDocument(LiteralArray(AIAssetDocumentJsonReader.Required(e, "items")))),
            "object" => ExactLiteral(e, ["kind", "properties"], () => new ObjectWorkflowLiteralDocument(Properties(AIAssetDocumentJsonReader.Required(e, "properties")))),
            _ => throw Unknown("workflow literal", kind)
        };
    }

    private static WorkflowLiteralDocument ExactLiteral(JsonElement e, string[] members, Func<WorkflowLiteralDocument> create)
    { Validate(e, members); return create(); }

    private static IReadOnlyList<WorkflowLiteralDocument> LiteralArray(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Array)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'items' must be an array.", "items");
        return e.EnumerateArray().Select(ReadLiteral).ToArray();
    }

    private static IReadOnlyList<WorkflowLiteralPropertyDocument> Properties(JsonElement e)
    {
        if (e.ValueKind != JsonValueKind.Array)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'properties' must be an array.", "properties");
        return e.EnumerateArray().Select(item =>
        {
            Validate(item, ["name", "value"]);
            return new WorkflowLiteralPropertyDocument(String(item, "name"), ReadLiteral(AIAssetDocumentJsonReader.Required(item, "value")));
        }).ToArray();
    }

    private static string ReadKind(JsonElement e)
    {
        var value = AIAssetDocumentJsonReader.Required(e, "kind");
        if (value.ValueKind != JsonValueKind.String)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'kind' must be a string.", "kind");
        return value.GetString()!;
    }

    private static void EnsureObjectAndDiscriminator(JsonElement e, string context)
    {
        if (e.ValueKind != JsonValueKind.Object)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Workflow {context} must be an object.", context);

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var hasKind = false;

        foreach (var property in e.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw AIAssetDocumentJsonReader.Codec(
                    AIAssetDocumentCodecFailureReason.DuplicateMember,
                    $"Duplicate member '{property.Name}'.",
                    property.Name);
            }

            if (StringComparer.Ordinal.Equals(property.Name, "kind"))
            {
                hasKind = true;
            }
        }

        if (!hasKind)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.MissingRequiredMember, "Required member 'kind' is missing.", "kind");

        var kind = AIAssetDocumentJsonReader.Required(e, "kind");
        if (kind.ValueKind != JsonValueKind.String)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'kind' must be a string.", "kind");
    }

    private static void Validate(JsonElement e, string[] members) => AIAssetDocumentJsonReader.EnsureObject(e, "workflow", members, members);
    private static string String(JsonElement e, string name) => AIAssetDocumentJsonReader.ReadNullableString(e, name)!;

    private static bool Boolean(JsonElement e, string name)
    {
        var value = AIAssetDocumentJsonReader.Required(e, name);
        if (value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Member '{name}' must be boolean.", name);
        return value.GetBoolean();
    }

    private static long Int64(JsonElement e, string name)
    {
        var value = AIAssetDocumentJsonReader.Required(e, name);
        if (value.ValueKind != JsonValueKind.Number)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Member '{name}' must be numeric.", name);
        if (!value.TryGetInt64(out var result))
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidScalarValue, $"Member '{name}' is outside Int64 range or is not an integer.", name);
        return result;
    }

    private static decimal Decimal(JsonElement e, string name)
    {
        var value = AIAssetDocumentJsonReader.Required(e, name);
        if (value.ValueKind != JsonValueKind.Number)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Member '{name}' must be numeric.", name);
        if (!value.TryGetDecimal(out var result))
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidScalarValue, $"Member '{name}' is outside Decimal range.", name);
        return result;
    }

    private static AIAssetDocumentCodecException Unknown(string context, string kind) =>
        AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.UnknownDiscriminator, $"Unknown {context} discriminator '{kind}'.", "kind", kind);
}
