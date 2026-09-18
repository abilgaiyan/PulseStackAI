using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Models;
using PulseStack.Core.Assets;
using Xunit;

namespace PulseStack.Tests.Assets;

public sealed class ModelAssetFactoryTests
{
    [Fact]
    public void Create_ShouldCreateModelAsset_WithGeneratedIdentityAndUrn()
    {
        var options = new ModelAssetOptions("TestProvider", "test-model");
        var factory = new ModelAssetFactory(new TestModelCatalog(options));

        var asset = factory.Create(options);

        asset.Id.Should().NotBe(AssetId.Empty);
        asset.Urn.Should().Be(new AssetUrn("urn:pulsestack:model:TestProvider:test-model"));
        asset.Version.Should().Be(AssetVersion.Initial);
        asset.Type.Should().Be(AssetType.Model);
        asset.Options.Should().Be(options);
    }

    [Fact]
    public void Create_ShouldGenerateANewIdentity_ForEachAsset()
    {
        var options = new ModelAssetOptions("TestProvider", "test-model");
        var factory = new ModelAssetFactory(new TestModelCatalog(options));

        var first = factory.Create(options);
        var second = factory.Create(options);

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Create_ShouldUseExplicitIdentity_WhenProvided()
    {
        var options = new ModelAssetOptions("TestProvider", "test-model");
        var factory = new ModelAssetFactory(new TestModelCatalog(options));
        var id = new AssetId(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        var asset = factory.Create(id, options);

        asset.Id.Should().Be(id);
        asset.Urn.Should().Be(new AssetUrn("urn:pulsestack:model:TestProvider:test-model"));
    }

    [Fact]
    public void Create_ShouldRejectEmptyExplicitIdentity()
    {
        var options = new ModelAssetOptions("TestProvider", "test-model");
        var factory = new ModelAssetFactory(new TestModelCatalog(options));

        var action = () => factory.Create(AssetId.Empty, options);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*AssetId cannot be empty*");
    }

    [Fact]
    public void Create_ShouldRejectAnUnknownProviderModelCombination()
    {
        var factory = new ModelAssetFactory(new TestModelCatalog());
        var options = new ModelAssetOptions("TestProvider", "unknown-model");

        var action = () => factory.Create(options);

        action.Should().Throw<ArgumentException>()
            .WithMessage("*unknown-model*TestProvider*");
    }

    private sealed class TestModelCatalog(params ModelAssetOptions[] models) : IModelCatalog
    {
        public IReadOnlyCollection<ProviderModelDescriptor> GetModels()
            => models
                .Select(model => new ProviderModelDescriptor(model.Provider, model.Model))
                .ToArray();

        public bool Contains(string provider, string model)
            => models.Any(candidate =>
                string.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Model, model, StringComparison.OrdinalIgnoreCase));
    }
}
