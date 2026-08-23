using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;
using Xunit;

namespace PulseStack.Tests.Assets;

public sealed class PromptAssetFactoryTests
{
    [Fact]
    public void Create_ShouldCreatePromptAsset_WithGeneratedIdentityAndUrn()
    {
        var options = new PromptAssetOptions
        {
            Name = "System Prompt",
            SystemInstructions = "You are concise and helpful."
        };

        var factory = new PromptAssetFactory();

        var asset = factory.Create(options);

        asset.Id.Should().NotBe(AssetId.Empty);
        asset.Urn.Value.Should().StartWith("urn:pulsestack:prompt:");
        asset.Version.Should().Be(AssetVersion.Initial);
        asset.Type.Should().Be(AssetType.Prompt);
        asset.Metadata.Name.Should().Be("System Prompt");
        asset.Options.Should().Be(options);
    }

    [Fact]
    public void Create_ShouldRejectMissingSystemInstructions()
    {
        var factory = new PromptAssetFactory();

        var action = () => factory.Create(
            new PromptAssetOptions
            {
                Name = "System Prompt",
                SystemInstructions = ""
            });

        action.Should().Throw<ArgumentException>();
    }
}
