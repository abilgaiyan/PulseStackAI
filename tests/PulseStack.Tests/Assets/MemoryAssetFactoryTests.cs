using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;
using Xunit;

namespace PulseStack.Tests.Assets;

public sealed class MemoryAssetFactoryTests
{
    [Fact]
    public void Create_ShouldCreateMemoryAsset_WithGeneratedIdentityAndMetadata()
    {
        var factory = new MemoryAssetFactory();
        var options = new MemoryAssetOptions
        {
            Name = "Conversation Memory",
            Description = "Retains conversational context.",
            Tags = ["conversation"]
        };

        var asset = factory.Create(options);

        asset.Id.Should().NotBe(AssetId.Empty);
        asset.Type.Should().Be(AssetType.Memory);
        asset.Metadata.Name.Should().Be("Conversation Memory");
        asset.Metadata.Description.Should().Be("Retains conversational context.");
        asset.Options.Tags.Should().ContainSingle("conversation");
    }

    [Fact]
    public void Create_ShouldGenerateNewIdentity_ForEachMemoryAsset()
    {
        var factory = new MemoryAssetFactory();
        var options = new MemoryAssetOptions
        {
            Name = "Conversation Memory",
            Description = "Retains conversational context."
        };

        factory.Create(options).Id.Should().NotBe(factory.Create(options).Id);
    }
}
