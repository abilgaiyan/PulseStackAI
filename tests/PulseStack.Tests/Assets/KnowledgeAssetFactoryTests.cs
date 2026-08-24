using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;
using Xunit;

namespace PulseStack.Tests.Assets;

public sealed class KnowledgeAssetFactoryTests
{
    [Fact]
    public void Create_ShouldCreateKnowledgeAsset_WithGeneratedIdentityAndMetadata()
    {
        var factory = new KnowledgeAssetFactory();
        var options = new KnowledgeAssetOptions
        {
            Name = "Customer Knowledge",
            Description = "Customer reference knowledge.",
            Tags = ["customer"]
        };

        var asset = factory.Create(options);

        asset.Id.Should().NotBe(AssetId.Empty);
        asset.Type.Should().Be(AssetType.Knowledge);
        asset.Metadata.Name.Should().Be("Customer Knowledge");
        asset.Metadata.Description.Should().Be("Customer reference knowledge.");
        asset.Options.Tags.Should().ContainSingle("customer");
    }

    [Fact]
    public void Create_ShouldGenerateNewIdentity_ForEachKnowledgeAsset()
    {
        var factory = new KnowledgeAssetFactory();
        var options = new KnowledgeAssetOptions
        {
            Name = "Customer Knowledge",
            Description = "Customer reference knowledge."
        };

        factory.Create(options).Id.Should().NotBe(factory.Create(options).Id);
    }
}
