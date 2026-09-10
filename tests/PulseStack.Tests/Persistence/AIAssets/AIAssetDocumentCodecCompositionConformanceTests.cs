using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetDocumentCodecCompositionConformanceTests
{
    [Fact]
    public void AddAIAssetDocumentCodec_ShouldRegisterSingletonPublicCodec()
    {
        var services = new ServiceCollection();

        services.AddAIAssetDocumentCodec();

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IAIAssetDocumentCodec>();
        var second = provider.GetRequiredService<IAIAssetDocumentCodec>();

        first.Should().BeOfType<AIAssetDocumentCodec>();
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void PublicCodec_ShouldEmitExactCanonicalWorkflowGoldenVector()
    {
        IAIAssetDocumentCodec codec = new AIAssetDocumentCodec();
        var run = Run("run");
        var workflow = new WorkflowAssetDocument(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument { Id = "wf", Urn = "urn:wf", Version = "1" },
            new AIAssetMetadataDocument("Workflow"),
            AIAssetLifecycleDocument.Published,
            [
                new LoopStepDocument(
                    "loop",
                    "L",
                    new LiteralValueDocument(new ArrayWorkflowLiteralDocument([
                        new IntegerWorkflowLiteralDocument(1),
                        new DecimalWorkflowLiteralDocument(2.500m)])),
                    run),
                new SwitchStepDocument(
                    "switch",
                    "S",
                    new ContextItemValueDocument("route"),
                    [new SwitchCaseDocument("x", run)],
                    null)
            ]);

        var expected = "{\"assetType\":\"workflow\",\"dependencies\":[],\"identity\":{\"id\":\"wf\",\"urn\":\"urn:wf\",\"version\":\"1\"},\"lifecycle\":\"published\",\"metadata\":{\"author\":null,\"category\":null,\"description\":null,\"name\":\"Workflow\",\"tags\":[]},\"references\":[],\"schemaVersion\":\"1.0\",\"steps\":[{\"items\":{\"kind\":\"literal\",\"literal\":{\"items\":[{\"kind\":\"integer\",\"value\":1},{\"kind\":\"decimal\",\"value\":2.5}],\"kind\":\"array\"}},\"kind\":\"loop\",\"name\":\"L\",\"step\":{\"agent\":{\"assetId\":\"agent\",\"assetType\":\"agent\",\"urn\":\"urn:agent\",\"version\":\"1\"},\"kind\":\"run\",\"stepId\":\"run\"},\"stepId\":\"loop\"},{\"cases\":[{\"step\":{\"agent\":{\"assetId\":\"agent\",\"assetType\":\"agent\",\"urn\":\"urn:agent\",\"version\":\"1\"},\"kind\":\"run\",\"stepId\":\"run\"},\"value\":\"x\"}],\"defaultStep\":null,\"kind\":\"switch\",\"name\":\"S\",\"selector\":{\"key\":\"route\",\"kind\":\"contextItem\"},\"stepId\":\"switch\"}]}";

        codec.SerializeToString(workflow).Should().Be(expected);
        codec.Serialize(workflow).Should().Equal(new UTF8Encoding(false, true).GetBytes(expected));
    }

    [Fact]
    public void PublicCodec_ShouldCanonicalizeLosslessNonCanonicalInput()
    {
        IAIAssetDocumentCodec codec = new AIAssetDocumentCodec();
        const string input = " { \"schemaVersion\" : \"1.0\", \"references\" : [ ], \"metadata\" : { \"tags\":[], \"name\":\"Tool\", \"description\":null, \"category\":null, \"author\":null }, \"lifecycle\":\"published\", \"identity\":{\"version\":\"1\",\"urn\":\"urn:tool\",\"id\":\"tool\"}, \"dependencies\":[], \"assetT\\u0079pe\":\"tool\" } \n";
        const string expected = "{\"assetType\":\"tool\",\"dependencies\":[],\"identity\":{\"id\":\"tool\",\"urn\":\"urn:tool\",\"version\":\"1\"},\"lifecycle\":\"published\",\"metadata\":{\"author\":null,\"category\":null,\"description\":null,\"name\":\"Tool\",\"tags\":[]},\"references\":[],\"schemaVersion\":\"1.0\"}";

        var document = codec.Deserialize(input);

        codec.SerializeToString(document).Should().Be(expected);
    }

    [Fact]
    public void PublicCodec_ShouldRejectRootProviderButRoundTripReferenceProviderToken()
    {
        IAIAssetDocumentCodec codec = new AIAssetDocumentCodec();
        const string providerRoot = "{\"assetType\":\"provider\",\"dependencies\":[],\"identity\":{\"id\":\"p\",\"urn\":\"urn:p\",\"version\":\"1\"},\"lifecycle\":\"draft\",\"metadata\":{\"author\":null,\"category\":null,\"description\":null,\"name\":\"P\",\"tags\":[]},\"references\":[],\"schemaVersion\":\"1.0\"}";

        Action deserializeRoot = () => codec.Deserialize(providerRoot);
        deserializeRoot.Should().Throw<AIAssetDocumentCodecException>()
            .Which.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.UnsupportedDiscriminator);

        var project = new ProjectAssetDocument(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument { Id = "project", Urn = "urn:project", Version = "1" },
            new AIAssetMetadataDocument("Project"),
            AIAssetLifecycleDocument.Draft,
            Reference(AIAssetDocumentType.Provider),
            [Reference(AIAssetDocumentType.Provider)]);

        var canonical = codec.Serialize(project);
        var reconstructed = codec.Deserialize(canonical);

        codec.Serialize(reconstructed).Should().Equal(canonical);
        Encoding.UTF8.GetString(canonical).Should().Contain("\"assetType\":\"provider\"");
    }

    [Fact]
    public void PublicCodec_ShouldShareTheFrozenDepthBoundaryEndToEnd()
    {
        IAIAssetDocumentCodec codec = new AIAssetDocumentCodec();
        WorkflowStepDocument step = Run("leaf");
        for (var i = 0; i < 80; i++)
        {
            step = new RetryStepDocument($"retry-{i}", "R", step, 2);
        }

        var workflow = new WorkflowAssetDocument(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument { Id = "wf", Urn = "urn:wf", Version = "1" },
            new AIAssetMetadataDocument("Workflow"),
            AIAssetLifecycleDocument.Draft,
            [step]);

        var canonical = codec.Serialize(workflow);
        var reconstructed = codec.Deserialize(canonical);

        codec.Serialize(reconstructed).Should().Equal(canonical);
    }

    private static RunStepDocument Run(string stepId) => new(stepId, Reference(AIAssetDocumentType.Agent));

    private static AIAssetReferenceDocument Reference(AIAssetDocumentType type) => new()
    {
        AssetType = type,
        AssetId = type.ToString().ToLowerInvariant(),
        Urn = $"urn:{type.ToString().ToLowerInvariant()}",
        Version = "1"
    };
}
