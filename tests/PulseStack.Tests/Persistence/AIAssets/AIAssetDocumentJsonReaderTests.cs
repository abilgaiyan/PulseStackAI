using System.Reflection;
using System.Text;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetDocumentJsonReaderTests
{
    private const string DeserializerType = "PulseStack.Abstractions.Persistence.AIAssets.Serialization.AIAssetDocumentStrictJsonDeserializer";
    private const string SerializerType = "PulseStack.Abstractions.Persistence.AIAssets.Serialization.AIAssetDocumentCanonicalJsonSerializer";

    [Theory]
    [InlineData("project", typeof(ProjectAssetDocument), ",\"entryWorkflow\":null,\"ownedAssets\":[]")]
    [InlineData("library", typeof(LibraryAssetDocument), ",\"members\":[]")]
    [InlineData("package", typeof(PackageAssetDocument), ",\"members\":[]")]
    [InlineData("workflow", typeof(WorkflowAssetDocument), ",\"steps\":[]")]
    [InlineData("agent", typeof(AgentAssetDocument), ",\"goal\":\"g\",\"knowledge\":[],\"memory\":null,\"model\":null,\"policies\":[],\"prompt\":null,\"responsibilities\":[],\"role\":\"r\",\"tools\":[]")]
    [InlineData("prompt", typeof(PromptAssetDocument), ",\"systemInstructions\":\"s\"")]
    [InlineData("tool", typeof(ToolAssetDocument), "")]
    [InlineData("knowledge", typeof(KnowledgeAssetDocument), "")]
    [InlineData("memory", typeof(MemoryAssetDocument), "")]
    [InlineData("policy", typeof(PolicyAssetDocument), "")]
    [InlineData("model", typeof(ModelAssetDocument), ",\"model\":\"m\",\"provider\":\"p\"")]
    public void Deserialize_ShouldConstructEverySupportedRoot(string token, Type expectedType, string specific)
    {
        var json = "{\"assetType\":\"" + token + "\",\"dependencies\":[],\"identity\":{\"id\":\"id\",\"urn\":\"urn:x\",\"version\":\"1\"},\"lifecycle\":\"draft\",\"metadata\":{\"author\":null,\"category\":null,\"description\":null,\"name\":\"n\",\"tags\":[]},\"references\":[],\"schemaVersion\":\"1.0\"" + specific + "}";
        Deserialize(json).Should().BeOfType(expectedType);
    }

    [Fact]
    public void Deserialize_ShouldRoundTripCompleteRecursiveWorkflowGraph()
    {
        var run = new RunStepDocument("00000000-0000-0000-0000-000000000001", Reference(AIAssetDocumentType.Agent));
        var literal = new ObjectWorkflowLiteralDocument([
            new WorkflowLiteralPropertyDocument("p", new ArrayWorkflowLiteralDocument([
                new NullWorkflowLiteralDocument(), new StringWorkflowLiteralDocument("café"),
                new BooleanWorkflowLiteralDocument(true), new IntegerWorkflowLiteralDocument(-7),
                new DecimalWorkflowLiteralDocument(12.3400m)]))
        ]);
        var workflow = new WorkflowAssetDocument(AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument { Id = "wf", Urn = "urn:wf", Version = "1" },
            new AIAssetMetadataDocument("Workflow"), AIAssetLifecycleDocument.Published,
            [
                new ParallelStepDocument("00000000-0000-0000-0000-000000000002", "p", [run]),
                new ConditionalStepDocument("00000000-0000-0000-0000-000000000003", "c", new NamedConditionDocument("ready"), run, null),
                new RetryStepDocument("00000000-0000-0000-0000-000000000004", "r", run, 3),
                new LoopStepDocument("00000000-0000-0000-0000-000000000005", "l", new LiteralValueDocument(literal), run),
                new SwitchStepDocument("00000000-0000-0000-0000-000000000006", "s", new ContextItemValueDocument("key"), [new SwitchCaseDocument("x", run)], run)
            ]);

        var canonical = Serialize(workflow);
        var reconstructed = Deserialize(canonical);
        Serialize(reconstructed).Should().Be(canonical);
        reconstructed.Should().BeEquivalentTo(workflow);
    }

    [Fact]
    public void Deserialize_ShouldRejectNestedUnknownAndDecodedDuplicateMembers()
    {
        var baseJson = "{\"assetType\":\"workflow\",\"dependencies\":[],\"identity\":{\"id\":\"id\",\"urn\":\"urn:x\",\"version\":\"1\"},\"lifecycle\":\"draft\",\"metadata\":{\"author\":null,\"category\":null,\"description\":null,\"name\":\"n\",\"tags\":[]},\"references\":[],\"schemaVersion\":\"1.0\",\"steps\":[{\"agent\":{\"assetId\":\"a\",\"assetType\":\"agent\",\"urn\":\"u\",\"version\":\"1\"},\"kind\":\"run\",\"stepId\":\"s\"}]}";

        Failure(baseJson.Replace("\"stepId\":\"s\"", "\"stepId\":\"s\",\"future\":1"))
            .FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.UnknownMember);
        Failure(baseJson.Replace("\"stepId\":\"s\"", "\"stepId\":\"s\",\"step\\u0049d\":\"s\""))
            .FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.DuplicateMember);
    }

    [Fact]
    public void Deserialize_ShouldClassifyWorkflowDiscriminatorAndScalarFailures()
    {
        var prefix = "{\"assetType\":\"workflow\",\"dependencies\":[],\"identity\":{\"id\":\"id\",\"urn\":\"urn:x\",\"version\":\"1\"},\"lifecycle\":\"draft\",\"metadata\":{\"author\":null,\"category\":null,\"description\":null,\"name\":\"n\",\"tags\":[]},\"references\":[],\"schemaVersion\":\"1.0\",\"steps\":[";
        Failure(prefix + "{\"kind\":\"future\",\"stepId\":\"s\"}]}").FailureReason
            .Should().Be(AIAssetDocumentCodecFailureReason.UnknownDiscriminator);
        Failure(prefix + "{\"kind\":\"retry\",\"maxAttempts\":999999999999,\"name\":\"r\",\"step\":{\"agent\":{\"assetId\":\"a\",\"assetType\":\"agent\",\"urn\":\"u\",\"version\":\"1\"},\"kind\":\"run\",\"stepId\":\"s\"},\"stepId\":\"r\"}]}").FailureReason
            .Should().Be(AIAssetDocumentCodecFailureReason.InvalidScalarValue);
    }

    private static AIAssetReferenceDocument Reference(AIAssetDocumentType type) => new()
    { AssetType = type, AssetId = "agent", Urn = "urn:agent", Version = "1" };

    private static AIAssetDocument Deserialize(string json)
    {
        var type = typeof(IAIAssetDocumentCodec).Assembly.GetType(DeserializerType, true)!;
        var method = type.GetMethod("Deserialize", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(string)], null)!;
        return (AIAssetDocument)method.Invoke(null, [json])!;
    }

    private static string Serialize(AIAssetDocument document)
    {
        var type = typeof(IAIAssetDocumentCodec).Assembly.GetType(SerializerType, true)!;
        var method = type.GetMethod("Serialize", BindingFlags.Static | BindingFlags.NonPublic, null, [typeof(AIAssetDocument)], null)!;
        return Encoding.UTF8.GetString((byte[])method.Invoke(null, [document])!);
    }

    private static AIAssetDocumentCodecException Failure(string json)
    {
        Action action = () => Deserialize(json);
        return action.Should().Throw<TargetInvocationException>().Which.InnerException.Should()
            .BeOfType<AIAssetDocumentCodecException>().Which;
    }
}
