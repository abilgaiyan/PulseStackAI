using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;
using Xunit;

namespace PulseStack.Tests.Assets;

public sealed class PolicyAssetFactoryTests
{
    [Fact]
    public void Create_ShouldCreatePolicyAsset_WithGeneratedIdentityAndMetadata()
    {
        var factory = new PolicyAssetFactory();
        var options = new PolicyAssetOptions
        {
            Name = "Restricted Tools",
            Description = "Restricts sensitive tool usage.",
            Tags = ["governance"]
        };

        var asset = factory.Create(options);

        asset.Id.Should().NotBe(AssetId.Empty);
        asset.Type.Should().Be(AssetType.Policy);
        asset.Metadata.Name.Should().Be("Restricted Tools");
        asset.Metadata.Description.Should().Be("Restricts sensitive tool usage.");
        asset.Options.Tags.Should().ContainSingle("governance");
    }

    [Fact]
    public void Create_ShouldGenerateNewIdentity_ForEachPolicyAsset()
    {
        var factory = new PolicyAssetFactory();
        var options = new PolicyAssetOptions
        {
            Name = "Restricted Tools",
            Description = "Restricts sensitive tool usage."
        };

        factory.Create(options).Id.Should().NotBe(factory.Create(options).Id);
    }
}
