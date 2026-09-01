using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class AIAssetDocumentMapperMetadataConsistencyTests
{
    private readonly AIAssetDocumentMapper mapper = new();

    [Fact]
    public void ToDocument_ShouldRejectPrompt_WhenNameDiffersOnlyByCase()
    {
        var source = new PromptAssetFactory().Create(
            new PromptAssetOptions
            {
                Name = "Assistant Prompt",
                SystemInstructions = "Be precise."
            });
        var asset = source with
        {
            Metadata = source.Metadata with { Name = "assistant Prompt" }
        };

        var action = () => mapper.ToDocument(asset);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Prompt*'Name'*canonical Metadata*");
    }

    [Fact]
    public void ToDocument_ShouldRejectTool_WhenCategoryDivergesFromMetadata()
    {
        var source = new ToolAssetFactory().Create(
            new ToolAssetOptions
            {
                Name = "Calculator",
                Description = "Performs calculations.",
                Category = "Utilities",
                Tags = ["math"]
            });
        var asset = source with
        {
            Metadata = source.Metadata with { Category = "Business" }
        };

        var action = () => mapper.ToDocument(asset);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Tool*'Category'*canonical Metadata*");
    }

    [Fact]
    public void ToDocument_ShouldRejectKnowledge_WhenDescriptionDivergesFromMetadata()
    {
        var source = new KnowledgeAssetFactory().Create(
            new KnowledgeAssetOptions
            {
                Name = "Customer Knowledge",
                Description = "Customer reference material.",
                Tags = ["customer"]
            });
        var asset = source with
        {
            Metadata = source.Metadata with { Description = "Published description" }
        };

        var action = () => mapper.ToDocument(asset);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Knowledge*'Description'*canonical Metadata*");
    }

    [Theory]
    [InlineData("Tool")]
    [InlineData("Knowledge")]
    [InlineData("Memory")]
    [InlineData("Policy")]
    public void ToDocument_ShouldRejectCollectionAsset_WhenTagOrderDivergesFromMetadata(
        string assetKind)
    {
        var source = CreateCollectionAsset(assetKind);
        var asset = source with
        {
            Metadata = source.Metadata with { Tags = ["second", "first"] }
        };

        var action = () => mapper.ToDocument(asset);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{assetKind}*'Tags'*canonical Metadata*");
    }

    private static Asset CreateCollectionAsset(string assetKind)
        => assetKind switch
        {
            "Tool" => new ToolAssetFactory().Create(
                new ToolAssetOptions
                {
                    Name = "Calculator",
                    Description = "Performs calculations.",
                    Category = "Utilities",
                    Tags = ["first", "second"]
                }),

            "Knowledge" => new KnowledgeAssetFactory().Create(
                new KnowledgeAssetOptions
                {
                    Name = "Customer Knowledge",
                    Description = "Customer reference material.",
                    Tags = ["first", "second"]
                }),

            "Memory" => new MemoryAssetFactory().Create(
                new MemoryAssetOptions
                {
                    Name = "Conversation Memory",
                    Description = "Retains conversation context.",
                    Tags = ["first", "second"]
                }),

            "Policy" => new PolicyAssetFactory().Create(
                new PolicyAssetOptions
                {
                    Name = "Privacy Policy",
                    Description = "Protects customer data.",
                    Tags = ["first", "second"]
                }),

            _ => throw new ArgumentOutOfRangeException(nameof(assetKind), assetKind, null)
        };
}
