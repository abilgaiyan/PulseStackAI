using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;
using Xunit;

namespace PulseStack.Tests.Assets;

public sealed class ToolAssetFactoryTests
{
    [Fact]
    public void Create_ShouldCreateToolAsset_WithGeneratedIdentityAndMetadata()
    {
        var factory = new ToolAssetFactory();
        var options = new ToolAssetOptions
        {
            Name = "Calculator",
            Description = "Performs calculations.",
            Category = "Utilities",
            Tags = ["math"]
        };

        var asset = factory.Create(options);

        asset.Id.Should().NotBe(AssetId.Empty);
        asset.Type.Should().Be(AssetType.Tool);
        asset.Metadata.Name.Should().Be("Calculator");
        asset.Metadata.Description.Should().Be("Performs calculations.");
        asset.Metadata.Category.Should().Be("Utilities");
        asset.Options.Tags.Should().ContainSingle("math");
    }

    [Fact]
    public void Create_ShouldGenerateNewIdentity_ForEachToolAsset()
    {
        var factory = new ToolAssetFactory();
        var options = new ToolAssetOptions
        {
            Name = "Calculator",
            Description = "Performs calculations.",
            Category = "Utilities"
        };

        factory.Create(options).Id.Should().NotBe(factory.Create(options).Id);
    }
}
