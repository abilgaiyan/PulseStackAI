using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class FoundationAssetDocumentTests
{
    [Fact]
    public void Constructors_ShouldUseTheirFrozenSchemaDiscriminators()
    {
        CreatePrompt("Be helpful.").AssetType.Should().Be(AIAssetDocumentType.Prompt);
        CreateTool().AssetType.Should().Be(AIAssetDocumentType.Tool);
        CreateKnowledge().AssetType.Should().Be(AIAssetDocumentType.Knowledge);
        CreateMemory().AssetType.Should().Be(AIAssetDocumentType.Memory);
        CreatePolicy().AssetType.Should().Be(AIAssetDocumentType.Policy);
        CreateModel("openai", "gpt-test").AssetType.Should().Be(AIAssetDocumentType.Model);
    }

    [Fact]
    public async Task Validator_ShouldAcceptEveryImplementedFoundationTypeRelationship()
    {
        var validator = new AIAssetDocumentValidator();
        AIAssetDocument[] documents =
        [
            CreatePrompt("Be helpful."),
            CreateTool(),
            CreateKnowledge(),
            CreateMemory(),
            CreatePolicy(),
            CreateModel("openai", "gpt-test")
        ];

        foreach (var document in documents)
        {
            var result = await validator.ValidateAsync(document);

            result.Errors.Should().NotContain(error =>
                error.Code == AIAssetDocumentValidationCodes.AssetTypeMismatch);
        }
    }

    [Fact]
    public void PromptDocument_ShouldHaveStructuralValueEquality()
    {
        var identity = CreateIdentity();
        var first = CreatePrompt("Be helpful.", identity);
        var second = CreatePrompt("Be helpful.", identity);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void ModelDocument_ShouldHaveStructuralValueEquality()
    {
        var identity = CreateIdentity();
        var first = CreateModel("openai", "gpt-test", identity);
        var second = CreateModel("openai", "gpt-test", identity);

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public async Task Validator_ShouldReportFoundationPayloadErrors()
    {
        var validator = new AIAssetDocumentValidator();

        var promptResult = await validator.ValidateAsync(CreatePrompt(" "));
        var modelResult = await validator.ValidateAsync(CreateModel("", " "));

        promptResult.Errors.Should().ContainSingle(error =>
            error.Code == AIAssetDocumentValidationCodes.MissingPromptSystemInstructions
            && error.Path == "$.systemInstructions");

        modelResult.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.MissingModelProvider
            && error.Path == "$.provider");
        modelResult.Errors.Should().Contain(error =>
            error.Code == AIAssetDocumentValidationCodes.MissingModelName
            && error.Path == "$.model");
    }

    private static PromptAssetDocument CreatePrompt(
        string systemInstructions,
        AIAssetIdentityDocument? identity = null)
        => new(
            AIAssetSchemaVersion.V1,
            identity ?? CreateIdentity(),
            new AIAssetMetadataDocument("System Prompt"),
            AIAssetLifecycleDocument.Draft,
            systemInstructions);

    private static ToolAssetDocument CreateTool()
        => new(
            AIAssetSchemaVersion.V1,
            CreateIdentity(),
            new AIAssetMetadataDocument("Search Tool"),
            AIAssetLifecycleDocument.Draft);

    private static KnowledgeAssetDocument CreateKnowledge()
        => new(
            AIAssetSchemaVersion.V1,
            CreateIdentity(),
            new AIAssetMetadataDocument("Product Knowledge"),
            AIAssetLifecycleDocument.Draft);

    private static MemoryAssetDocument CreateMemory()
        => new(
            AIAssetSchemaVersion.V1,
            CreateIdentity(),
            new AIAssetMetadataDocument("Conversation Memory"),
            AIAssetLifecycleDocument.Draft);

    private static PolicyAssetDocument CreatePolicy()
        => new(
            AIAssetSchemaVersion.V1,
            CreateIdentity(),
            new AIAssetMetadataDocument("Safety Policy"),
            AIAssetLifecycleDocument.Draft);

    private static ModelAssetDocument CreateModel(
        string provider,
        string model,
        AIAssetIdentityDocument? identity = null)
        => new(
            AIAssetSchemaVersion.V1,
            identity ?? CreateIdentity(),
            new AIAssetMetadataDocument(model.Length == 0 ? "Model" : model),
            AIAssetLifecycleDocument.Draft,
            provider,
            model);

    private static AIAssetIdentityDocument CreateIdentity()
        => new()
        {
            Id = Guid.NewGuid().ToString(),
            Urn = "urn:pulsestack:test:asset",
            Version = "1.0.0"
        };
}
