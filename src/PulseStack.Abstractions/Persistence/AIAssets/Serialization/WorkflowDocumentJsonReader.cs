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
            "run" => ReadRun(element),
            "parallel" => ReadParallel(element),
            "conditional" => ReadConditional(element),
            "retry" => ReadRetry(element),
            "loop" => ReadLoop(element),
            "switch" => ReadSwitch(element),
            _ => throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.UnknownDiscriminator,
                $"Unknown workflow step discriminator '{kind}'.", "kind", kind)
        };
    }

    private static RunStepDocument ReadRun(JsonElement element)
    {
        Validate(element, ["agent", "kind", "stepId"], ["agent", "kind", "stepId"]);
        return new RunStepDocument(String(element, "stepId"), AIAssetDocumentJsonReader.ReadReference(AIAssetDocumentJsonReader.Required(element, "agent")));
    }

    private static ParallelStepDocument ReadParallel(JsonElement element)
    {
        Validate(element, ["kind", "name", "steps", "stepId"], ["kind", "name", "steps", "stepId"]);
        return new ParallelStepDocument(String(element, "stepId"), String(element, "name"), ReadSteps(AIAssetDocumentJsonReader.Required(element, "steps")));
    }

    private static ConditionalStepDocument ReadConditional(JsonElement element)
    {
        Validate(element, ["condition", "elseStep", "kind", "name", "stepId", "thenStep"], ["condition", "elseStep", "kind", "name", "stepId", "thenStep"]);
        var otherwise = AIAssetDocumentJsonReader.Required(element, "elseStep");
        return new ConditionalStepDocument(String(element, "stepId"), String(element, "name"),
            ReadCondition(AIAssetDocumentJsonReader.Required(element, "condition")),
            ReadStep(AIAssetDocumentJsonReader.Required(element, "thenStep")),
            otherwise.ValueKind == JsonValueKind.Null ? null : ReadStep(otherwise));
    }

    private static RetryStepDocument ReadRetry(JsonElement element)
    {
        Validate(element, ["kind", "maxAttempts", "name", "step", "stepId"], ["kind", "maxAttempts", "name", "step", "stepId"]);
        var attempts = AIAssetDocumentJsonReader.Required(element, "maxAttempts");
        if (attempts.ValueKind != JsonValueKind.Number || !attempts.TryGetInt32(out var value))
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidScalarValue,
                "Member 'maxAttempts' must be a representable Int32 JSON number.", "maxAttempts");
        return new RetryStepDocument(String(element, "stepId"), String(element, "name"), ReadStep(AIAssetDocumentJsonReader.Required(element, "step")), value);
    }

    private static LoopStepDocument ReadLoop(JsonElement element)
    {
        Validate(element, ["items", "kind", "name", "step", "stepId"], ["items", "kind", "name", "step", "stepId"]);
        return new LoopStepDocument(String(element, "stepId"), String(element, "name"),
            ReadValue(AIAssetDocumentJsonReader.Required(element, "items")), ReadStep(AIAssetDocumentJsonReader.Required(element, "step")));
    }

    private static SwitchStepDocument ReadSwitch(JsonElement element)
    {
        Validate(element, ["cases", "defaultStep", "kind", "name", "selector", "stepId"], ["cases", "defaultStep", "kind", "name", "selector", "stepId"]);
        var casesElement = AIAssetDocumentJsonReader.Required(element, "cases");
        if (casesElement.ValueKind != JsonValueKind.Array)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'cases' must be an array.", "cases");
        var cases = casesElement.EnumerateArray().Select(ReadCase).ToArray();
        var defaultStep = AIAssetDocumentJsonReader.Required(element, "defaultStep");
        return new SwitchStepDocument(String(element, "stepId"), String(element, "name"),
            ReadValue(AIAssetDocumentJsonReader.Required(element, "selector")), cases,
            defaultStep.ValueKind == JsonValueKind.Null ? null : ReadStep(defaultStep));
    }

    private static SwitchCaseDocument ReadCase(JsonElement element)
    {
        Validate(element, ["step", "value"], ["step", "value"]);
        return new SwitchCaseDocument(String(element, "value"), ReadStep(AIAssetDocumentJsonReader.Required(element, "step")));
    }

    private static WorkflowConditionDocument ReadCondition(JsonElement element)
    {
        EnsureObjectAndDiscriminator(element, "condition");
        var kind = ReadKind(element);
        if (!StringComparer.Ordinal.Equals(kind, "named"))
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.UnknownDiscriminator,
                $"Unknown workflow condition discriminator '{kind}'.", "kind", kind);
        Validate(element, ["kind", "name"], ["kind", "name"]);
        return new NamedConditionDocument(String(element, "name"));
    }

    private static WorkflowValueDocument ReadValue(JsonElement element)
    {
        EnsureObjectAndDiscriminator(element, "value");
        var kind = ReadKind(element);
        return kind switch
        {
            "input" => ExactValue<InputValueDocument>(element, ["kind"], () => new InputValueDocument()),
            "currentOutput" => ExactValue<CurrentOutputValueDocument>(element, ["kind"], () => new CurrentOutputValueDocument()),
            "contextItem" => ExactValue<ContextItemValueDocument>(element, ["key", "kind"], () => new ContextItemValueDocument(String(element, "key"))),
            "literal" => ExactValue<LiteralValueDocument>(element, ["kind", "literal"], () => new LiteralValueDocument(ReadLiteral(AIAssetDocumentJsonReader.Required(element, "literal")))),
            _ => throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.UnknownDiscriminator,
                $"Unknown workflow value discriminator '{kind}'.", "kind", kind)
        };
    }

    private static T ExactValue<T>(JsonElement element, string[] members, Func<T> factory) where T : WorkflowValueDocument
    {
        Validate(element, members, members);
        return factory();
    }

    private static WorkflowLiteralDocument ReadLiteral(JsonElement element)
    {
        EnsureObjectAndDiscriminator(element, "literal");
        var kind = ReadKind(element);
        return kind switch
        {
            "null" => ExactLiteral(element, ["kind"], () => new NullWorkflowLiteralDocument()),
            "string" => ExactLiteral(element, ["kind", "value"], () => new StringWorkflowLiteralDocument(String(element, "value"))),
            "boolean" => ExactLiteral(element, ["kind", "value"], () => new BooleanWorkflowLiteralDocument(Boolean(element, "value"))),
            "integer" => ExactLiteral(element, ["kind", "value"], () => new IntegerWorkflowLiteralDocument(Int64(element, "value"))),
            "decimal" => ExactLiteral(element, ["kind", "value"], () => new DecimalWorkflowLiteralDocument(Decimal(element, "value"))),
            "array" => ExactLiteral(element, ["items", "kind"], () => new ArrayWorkflowLiteralDocument(LiteralArray(AIAssetDocumentJsonReader.Required(element, "items")))),
            "object" => ExactLiteral(element, ["kind", "properties"], () => new ObjectWorkflowLiteralDocument(Properties(AIAssetDocumentJsonReader.Required(element, "properties")))),
            _ => throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.UnknownDiscriminator,
                $"Unknown workflow literal discriminator '{kind}'.", "kind", kind)
        };
    }

    private static WorkflowLiteralDocument ExactLiteral(JsonElement element, string[] members, Func<WorkflowLiteralDocument> factory)
    {
        Validate(element, members, members);
        return factory();
    }

    private static IReadOnlyList<WorkflowLiteralDocument> LiteralArray(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'items' must be an array.", "items");
        return element.EnumerateArray().Select(ReadLiteral).ToArray();
    }

    private static IReadOnlyList<WorkflowLiteralPropertyDocument> Properties(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'properties' must be an array.", "properties");
        return element.EnumerateArray().Select(item =>
        {
            Validate(item, ["name", "value"], ["name", "value"]);
            return new WorkflowLiteralPropertyDocument(String(item, "name"), ReadLiteral(AIAssetDocumentJsonReader.Required(item, "value")));
        }).ToArray();
    }

    private static string ReadKind(JsonElement element)
    {
        var value = AIAssetDocumentJsonReader.Required(element, "kind");
        if (value.ValueKind != JsonValueKind.String)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'kind' must be a string.", "kind");
        return value.GetString()!;
    }

    private static void EnsureObjectAndDiscriminator(JsonElement element, string context)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Workflow {context} must be an object.", context);
        var count = element.EnumerateObject().Count(p => StringComparer.Ordinal.Equals(p.Name, "kind"));
        if (count == 0)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.MissingRequiredMember, "Required member 'kind' is missing.", "kind");
        if (count > 1)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.DuplicateMember, "Duplicate member 'kind'.", "kind");
    }

    private static void Validate(JsonElement element, string[] allowed, string[] required) =>
        AIAssetDocumentJsonReader.EnsureObject(element, "workflow", allowed, required);

    private static string String(JsonElement obj, string name) => AIAssetDocumentJsonReader.ReadNullableString(obj, name)!;

    private static bool Boolean(JsonElement obj, string name)
    {
        var value = AIAssetDocumentJsonReader.Required(obj, name);
        if (value.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Member '{name}' must be boolean.", name);
        return value.GetBoolean();
    }

    private static long Int64(JsonElement obj, string name)
    {
        var value = AIAssetDocumentJsonReader.Required(obj, name);
        if (value.ValueKind != JsonValueKind.Number)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Member '{name}' must be numeric.", name);
        if (!value.TryGetInt64(out var result))
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidScalarValue, $"Member '{name}' is outside Int64 range.", name);
        return result;
    }

    private static decimal Decimal(JsonElement obj, string name)
    {
        var value = AIAssetDocumentJsonReader.Required(obj, name);
        if (value.ValueKind != JsonValueKind.Number)
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Member '{name}' must be numeric.", name);
        if (!value.TryGetDecimal(out var result))
            throw AIAssetDocumentJsonReader.Codec(AIAssetDocumentCodecFailureReason.InvalidScalarValue, $"Member '{name}' is outside Decimal range.", name);
        return result;
    }
}
