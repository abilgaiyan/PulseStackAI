using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class WorkflowDocumentCanonicalJsonWriterTests
{
    private const string SerializerType = "PulseStack.Abstractions.Persistence.AIAssets.Serialization.AIAssetDocumentCanonicalJsonSerializer";

    [Fact]
    public void Serialize_ShouldWriteAllStepKindsRecursivelyWithOrdinalMembers()
    {
        var run = Run("00000000-0000-0000-0000-000000000001");
        var steps = new WorkflowStepDocument[]
        {
            run,
            new ParallelStepDocument("00000000-0000-0000-0000-000000000002", "p", [run]),
            new ConditionalStepDocument("00000000-0000-0000-0000-000000000003", "c", new NamedConditionDocument("ready"), run, null),
            new RetryStepDocument("00000000-0000-0000-0000-000000000004", "r", run, 3),
            new LoopStepDocument("00000000-0000-0000-0000-000000000005", "l", new InputValueDocument(), run),
            new SwitchStepDocument("00000000-0000-0000-0000-000000000006", "s", new CurrentOutputValueDocument(), [new SwitchCaseDocument("x", run)], run)
        };

        var json = Serialize(Workflow(steps));

        json.Should().Contain("{\"agent\":{\"assetId\":\"agent-1\",\"assetType\":\"agent\",\"urn\":\"urn:pulsestack:agent:agent-1\",\"version\":\"1.0.0\"},\"kind\":\"run\",\"stepId\":\"00000000-0000-0000-0000-000000000001\"}");
        json.Should().Contain("\"kind\":\"parallel\",\"name\":\"p\",\"steps\":[");
        json.Should().Contain("\"condition\":{\"kind\":\"named\",\"name\":\"ready\"},\"elseStep\":null,\"kind\":\"conditional\"");
        json.Should().Contain("\"kind\":\"retry\",\"maxAttempts\":3,\"name\":\"r\"");
        json.Should().Contain("\"items\":{\"kind\":\"input\"},\"kind\":\"loop\"");
        json.Should().Contain("\"cases\":[{\"step\":");
        json.Should().Contain("\"defaultStep\":{\"agent\":");
        json.Should().Contain("\"selector\":{\"kind\":\"currentOutput\"}");
    }

    [Fact]
    public void Serialize_ShouldWriteValueAndLiteralVocabularyIncludingOrderedObjectProperties()
    {
        var literal = new ObjectWorkflowLiteralDocument([
            new WorkflowLiteralPropertyDocument("second", new ArrayWorkflowLiteralDocument([
                new NullWorkflowLiteralDocument(), new BooleanWorkflowLiteralDocument(true), new IntegerWorkflowLiteralDocument(-2), new DecimalWorkflowLiteralDocument(12.3400m)])),
            new WorkflowLiteralPropertyDocument("first", new StringWorkflowLiteralDocument("café"))
        ]);
        var loop = new LoopStepDocument("00000000-0000-0000-0000-000000000010", "values", new LiteralValueDocument(literal), Run("00000000-0000-0000-0000-000000000011"));
        var json = Serialize(Workflow([loop]));

        json.Should().Contain("\"kind\":\"literal\",\"literal\":{\"kind\":\"object\",\"properties\":[{\"name\":\"second\"");
        json.Should().Contain("{\"kind\":\"null\"},{\"kind\":\"boolean\",\"value\":true},{\"kind\":\"integer\",\"value\":-2},{\"kind\":\"decimal\",\"value\":12.34}");
        json.Should().Contain("{\"name\":\"first\",\"value\":{\"kind\":\"string\",\"value\":\"café\"}}");
    }

    [Fact]
    public void Serialize_ShouldCanonicalizeDecimalZeroWithoutExponentOrNegativeZero()
    {
        var values = new ArrayWorkflowLiteralDocument([
            new DecimalWorkflowLiteralDocument(0.000m),
            new DecimalWorkflowLiteralDocument(-0.000m),
            new DecimalWorkflowLiteralDocument(10000000000000000000000000000m)
        ]);
        var loop = new LoopStepDocument("00000000-0000-0000-0000-000000000020", "d", new LiteralValueDocument(values), Run("00000000-0000-0000-0000-000000000021"));
        var json = Serialize(Workflow([loop]));

        using var parsed = JsonDocument.Parse(json);
        var items = parsed.RootElement
            .GetProperty("steps")[0]
            .GetProperty("items")
            .GetProperty("literal")
            .GetProperty("items");

        items[0].GetProperty("value").GetRawText().Should().Be("0");
        items[1].GetProperty("value").GetRawText().Should().Be("0");
        items[2].GetProperty("value").GetRawText().Should().Be("10000000000000000000000000000");
    }

    [Fact]
    public void Serialize_ShouldRejectInvalidUnicodeInsideNestedWorkflowContent()
    {
        var invalid = new string(['\uD800']);
        var loop = new LoopStepDocument("00000000-0000-0000-0000-000000000030", invalid, new ContextItemValueDocument("key"), Run("00000000-0000-0000-0000-000000000031"));
        var exception = Failure(Workflow([loop]));
        exception.Operation.Should().Be(AIAssetDocumentCodecOperation.Serialization);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidUnicode);
        exception.MemberName.Should().Be("name");
    }

    private static RunStepDocument Run(string id) => new(id, new AIAssetReferenceDocument
    {
        AssetType = AIAssetDocumentType.Agent, AssetId = "agent-1", Urn = "urn:pulsestack:agent:agent-1", Version = "1.0.0"
    });

    private static WorkflowAssetDocument Workflow(IEnumerable<WorkflowStepDocument> steps) => new(
        AIAssetSchemaVersion.V1,
        new AIAssetIdentityDocument { Id = "workflow-1", Urn = "urn:pulsestack:workflow:workflow-1", Version = "1.0.0" },
        new AIAssetMetadataDocument("Workflow"), AIAssetLifecycleDocument.Draft, steps);

    private static string Serialize(AIAssetDocument document) => Encoding.UTF8.GetString((byte[])Invoke(document));

    private static AIAssetDocumentCodecException Failure(AIAssetDocument document)
    {
        Action action = () => Invoke(document);
        return action.Should().Throw<TargetInvocationException>().Which.InnerException.Should().BeOfType<AIAssetDocumentCodecException>().Which;
    }

    private static object Invoke(AIAssetDocument document)
    {
        var type = typeof(IAIAssetDocumentCodec).Assembly.GetType(SerializerType, true)!;
        var method = type.GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(AIAssetDocument)], null)!;
        return method.Invoke(null, [document])!;
    }
}
