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

public sealed class AIAssetDocumentCanonicalJsonSerializerTests
{
    private const string AuthorityTypeName =
        "PulseStack.Abstractions.Persistence.AIAssets.Serialization.AIAssetDocumentCanonicalJsonSerializer";

    [Fact]
    public void Serialize_ShouldEmitExactCanonicalSharedRootProfile()
    {
        var document = new ToolAssetDocument(
            AIAssetSchemaVersion.V1,
            Identity("tool-1"),
            Metadata("Tool"),
            AIAssetLifecycleDocument.Published);

        var bytes = Serialize(document);
        var json = Encoding.UTF8.GetString(bytes);

        json.Should().Be(
            "{\"assetType\":\"tool\",\"dependencies\":[],\"identity\":{\"id\":\"tool-1\",\"urn\":\"urn:pulsestack:tool:tool-1\",\"version\":\"1.0.0\"},\"lifecycle\":\"published\",\"metadata\":{\"author\":null,\"category\":null,\"description\":null,\"name\":\"Tool\",\"tags\":[]},\"references\":[],\"schemaVersion\":\"1.0\"}");

        bytes.Should().NotBeEmpty();
        bytes[0].Should().Be((byte)'{');
        (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            .Should().BeFalse();
        json.Should().NotEndWith("\n");
        json.Should().NotContain("\r");
    }

    [Fact]
    public void Serialize_ShouldEmitExactCanonicalAgentProfileAndNestedScalarVocabulary()
    {
        var provider = Reference(AIAssetDocumentType.Provider, "provider-1");
        var model = Reference(AIAssetDocumentType.Model, "model-1");
        var prompt = Reference(AIAssetDocumentType.Prompt, "prompt-1");
        var memory = Reference(AIAssetDocumentType.Memory, "memory-1");
        var knowledge = Reference(AIAssetDocumentType.Knowledge, "knowledge-1");
        var tool = Reference(AIAssetDocumentType.Tool, "tool-1");
        var policy = Reference(AIAssetDocumentType.Policy, "policy-1");

        var document = new AgentAssetDocument(
            AIAssetSchemaVersion.V1,
            Identity("agent-1"),
            new AIAssetMetadataDocument(
                "Agent",
                description: "desc",
                author: "author",
                tags: ["z", "a"],
                category: "category"),
            AIAssetLifecycleDocument.Validated,
            goal: "goal",
            role: "role",
            responsibilities: ["first", "second"],
            model: model,
            prompt: prompt,
            knowledge: [knowledge],
            tools: [tool],
            memory: memory,
            policies: [policy],
            references: [provider, model],
            dependencies:
            [
                new AIAssetDependencyDocument
                {
                    Reference = provider,
                    Required = false
                }
            ]);

        SerializeToString(document).Should().Be(
            "{\"assetType\":\"agent\",\"dependencies\":[{\"reference\":{\"assetId\":\"provider-1\",\"assetType\":\"provider\",\"urn\":\"urn:pulsestack:provider:provider-1\",\"version\":\"1.0.0\"},\"required\":false}],\"goal\":\"goal\",\"identity\":{\"id\":\"agent-1\",\"urn\":\"urn:pulsestack:agent:agent-1\",\"version\":\"1.0.0\"},\"knowledge\":[{\"assetId\":\"knowledge-1\",\"assetType\":\"knowledge\",\"urn\":\"urn:pulsestack:knowledge:knowledge-1\",\"version\":\"1.0.0\"}],\"lifecycle\":\"validated\",\"memory\":{\"assetId\":\"memory-1\",\"assetType\":\"memory\",\"urn\":\"urn:pulsestack:memory:memory-1\",\"version\":\"1.0.0\"},\"metadata\":{\"author\":\"author\",\"category\":\"category\",\"description\":\"desc\",\"name\":\"Agent\",\"tags\":[\"z\",\"a\"]},\"model\":{\"assetId\":\"model-1\",\"assetType\":\"model\",\"urn\":\"urn:pulsestack:model:model-1\",\"version\":\"1.0.0\"},\"policies\":[{\"assetId\":\"policy-1\",\"assetType\":\"policy\",\"urn\":\"urn:pulsestack:policy:policy-1\",\"version\":\"1.0.0\"}],\"prompt\":{\"assetId\":\"prompt-1\",\"assetType\":\"prompt\",\"urn\":\"urn:pulsestack:prompt:prompt-1\",\"version\":\"1.0.0\"},\"references\":[{\"assetId\":\"provider-1\",\"assetType\":\"provider\",\"urn\":\"urn:pulsestack:provider:provider-1\",\"version\":\"1.0.0\"},{\"assetId\":\"model-1\",\"assetType\":\"model\",\"urn\":\"urn:pulsestack:model:model-1\",\"version\":\"1.0.0\"}],\"responsibilities\":[\"first\",\"second\"],\"role\":\"role\",\"schemaVersion\":\"1.0\",\"tools\":[{\"assetId\":\"tool-1\",\"assetType\":\"tool\",\"urn\":\"urn:pulsestack:tool:tool-1\",\"version\":\"1.0.0\"}]}");
    }

    [Fact]
    public void Serialize_ShouldEmitExactRootMemberOrderForAllSupportedRootDocuments()
    {
        var documents = new (AIAssetDocument Document, string[] Members)[]
        {
            (
                new ProjectAssetDocument(AIAssetSchemaVersion.V1, Identity("project"), Metadata("Project"), AIAssetLifecycleDocument.Draft, null),
                ["assetType", "dependencies", "entryWorkflow", "identity", "lifecycle", "metadata", "ownedAssets", "references", "schemaVersion"]),
            (
                new LibraryAssetDocument(AIAssetSchemaVersion.V1, Identity("library"), Metadata("Library"), AIAssetLifecycleDocument.Draft),
                ["assetType", "dependencies", "identity", "lifecycle", "members", "metadata", "references", "schemaVersion"]),
            (
                new PackageAssetDocument(AIAssetSchemaVersion.V1, Identity("package"), Metadata("Package"), AIAssetLifecycleDocument.Draft),
                ["assetType", "dependencies", "identity", "lifecycle", "members", "metadata", "references", "schemaVersion"]),
            (
                new WorkflowAssetDocument(AIAssetSchemaVersion.V1, Identity("workflow"), Metadata("Workflow"), AIAssetLifecycleDocument.Draft),
                ["assetType", "dependencies", "identity", "lifecycle", "metadata", "references", "schemaVersion", "steps"]),
            (
                new AgentAssetDocument(AIAssetSchemaVersion.V1, Identity("agent"), Metadata("Agent"), AIAssetLifecycleDocument.Draft, "goal", "role"),
                ["assetType", "dependencies", "goal", "identity", "knowledge", "lifecycle", "memory", "metadata", "model", "policies", "prompt", "references", "responsibilities", "role", "schemaVersion", "tools"]),
            (
                new PromptAssetDocument(AIAssetSchemaVersion.V1, Identity("prompt"), Metadata("Prompt"), AIAssetLifecycleDocument.Draft, "system"),
                ["assetType", "dependencies", "identity", "lifecycle", "metadata", "references", "schemaVersion", "systemInstructions"]),
            (
                new ToolAssetDocument(AIAssetSchemaVersion.V1, Identity("tool"), Metadata("Tool"), AIAssetLifecycleDocument.Draft),
                ["assetType", "dependencies", "identity", "lifecycle", "metadata", "references", "schemaVersion"]),
            (
                new KnowledgeAssetDocument(AIAssetSchemaVersion.V1, Identity("knowledge"), Metadata("Knowledge"), AIAssetLifecycleDocument.Draft),
                ["assetType", "dependencies", "identity", "lifecycle", "metadata", "references", "schemaVersion"]),
            (
                new MemoryAssetDocument(AIAssetSchemaVersion.V1, Identity("memory"), Metadata("Memory"), AIAssetLifecycleDocument.Draft),
                ["assetType", "dependencies", "identity", "lifecycle", "metadata", "references", "schemaVersion"]),
            (
                new PolicyAssetDocument(AIAssetSchemaVersion.V1, Identity("policy"), Metadata("Policy"), AIAssetLifecycleDocument.Draft),
                ["assetType", "dependencies", "identity", "lifecycle", "metadata", "references", "schemaVersion"]),
            (
                new ModelAssetDocument(AIAssetSchemaVersion.V1, Identity("model"), Metadata("Model"), AIAssetLifecycleDocument.Draft, "provider", "model"),
                ["assetType", "dependencies", "identity", "lifecycle", "metadata", "model", "provider", "references", "schemaVersion"])
        };

        foreach (var (document, members) in documents)
        {
            using var parsed = JsonDocument.Parse(Serialize(document));
            parsed.RootElement.EnumerateObject().Select(property => property.Name)
                .Should().Equal(members);
        }
    }

    [Fact]
    public void Serialize_ShouldPreserveSemanticSequenceOrderAndExplicitNulls()
    {
        var first = Reference(AIAssetDocumentType.Tool, "first");
        var second = Reference(AIAssetDocumentType.Tool, "second");

        var project = new ProjectAssetDocument(
            AIAssetSchemaVersion.V1,
            Identity("project"),
            new AIAssetMetadataDocument("Project", tags: ["second", "first"]),
            AIAssetLifecycleDocument.Draft,
            entryWorkflow: null,
            ownedAssets: [second, first],
            references: [first, second]);

        using var parsed = JsonDocument.Parse(Serialize(project));
        var root = parsed.RootElement;

        root.GetProperty("entryWorkflow").ValueKind.Should().Be(JsonValueKind.Null);
        root.GetProperty("ownedAssets").EnumerateArray().Select(x => x.GetProperty("assetId").GetString())
            .Should().Equal("second", "first");
        root.GetProperty("references").EnumerateArray().Select(x => x.GetProperty("assetId").GetString())
            .Should().Equal("first", "second");
        root.GetProperty("metadata").GetProperty("tags").EnumerateArray().Select(x => x.GetString())
            .Should().Equal("second", "first");
    }

    [Fact]
    public void Serialize_ShouldEmitNonAsciiUtf8DirectlyAndUseJsonShortEscapes()
    {
        var prompt = new PromptAssetDocument(
            AIAssetSchemaVersion.V1,
            Identity("prompt"),
            Metadata("Café"),
            AIAssetLifecycleDocument.Draft,
            "café\n\t\"quoted\"\\end");

        var bytes = Serialize(prompt);
        var json = Encoding.UTF8.GetString(bytes);

        json.Should().Contain("Café");
        json.Should().Contain("café\\n\\t\\\"quoted\\\"\\\\end");
        json.Should().NotContain("\\u00e9");
        json.Should().NotContain("\\u00E9");
        SerializeToString(prompt).Should().Be(json);
        Encoding.UTF8.GetBytes(SerializeToString(prompt)).Should().Equal(bytes);
    }

    [Fact]
    public void Serialize_ShouldRejectInvalidUtf16WithoutRepair()
    {
        var invalid = new string(new[] { '\uD800' });
        var prompt = new PromptAssetDocument(
            AIAssetSchemaVersion.V1,
            Identity("prompt"),
            Metadata("Prompt"),
            AIAssetLifecycleDocument.Draft,
            invalid);

        var exception = InvokeCodecFailure(
            GetMethod("Serialize", typeof(AIAssetDocument)),
            prompt);

        exception.Operation.Should().Be(AIAssetDocumentCodecOperation.Serialization);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidUnicode);
        exception.MemberName.Should().Be("systemInstructions");
        exception.Token.Should().BeNull();
    }

    [Fact]
    public void Serialize_ShouldRejectOutOfVocabularyScalarEnumValues()
    {
        var tool = new ToolAssetDocument(
            AIAssetSchemaVersion.V1,
            Identity("tool"),
            Metadata("Tool"),
            (AIAssetLifecycleDocument)999);

        var exception = InvokeCodecFailure(
            GetMethod("Serialize", typeof(AIAssetDocument)),
            tool);

        exception.Operation.Should().Be(AIAssetDocumentCodecOperation.Serialization);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.UnrepresentableDocument);
        exception.MemberName.Should().Be("lifecycle");
        exception.Token.Should().Be("999");
    }

    private static byte[] Serialize(AIAssetDocument document)
    {
        return (byte[])Invoke(GetMethod("Serialize", typeof(AIAssetDocument)), document);
    }

    private static string SerializeToString(AIAssetDocument document)
    {
        return (string)Invoke(GetMethod("SerializeToString", typeof(AIAssetDocument)), document);
    }

    private static AIAssetIdentityDocument Identity(string id)
    {
        var type = id.Split('-', 2)[0];

        return new AIAssetIdentityDocument
        {
            Id = id,
            Urn = $"urn:pulsestack:{type}:{id}",
            Version = "1.0.0"
        };
    }

    private static AIAssetMetadataDocument Metadata(string name)
    {
        return new AIAssetMetadataDocument(name);
    }

    private static AIAssetReferenceDocument Reference(AIAssetDocumentType assetType, string id)
    {
        return new AIAssetReferenceDocument
        {
            AssetType = assetType,
            AssetId = id,
            Urn = $"urn:pulsestack:{assetType.ToString().ToLowerInvariant()}:{id}",
            Version = "1.0.0"
        };
    }

    private static MethodInfo GetMethod(string name, params Type[] parameterTypes)
    {
        var authority = typeof(IAIAssetDocumentCodec).Assembly.GetType(
            AuthorityTypeName,
            throwOnError: true)!;

        return authority.GetMethod(
                   name,
                   BindingFlags.Static | BindingFlags.NonPublic,
                   binder: null,
                   types: parameterTypes,
                   modifiers: null)
               ?? throw new InvalidOperationException($"Method '{name}' was not found.");
    }

    private static object Invoke(MethodInfo method, params object?[] arguments)
    {
        return method.Invoke(null, arguments)
               ?? throw new InvalidOperationException("Canonical serializer returned null.");
    }

    private static AIAssetDocumentCodecException InvokeCodecFailure(
        MethodInfo method,
        params object?[] arguments)
    {
        Action action = () => method.Invoke(null, arguments);

        var invocationException = action.Should().Throw<TargetInvocationException>().Which;
        return invocationException.InnerException.Should()
            .BeOfType<AIAssetDocumentCodecException>()
            .Which;
    }
}
