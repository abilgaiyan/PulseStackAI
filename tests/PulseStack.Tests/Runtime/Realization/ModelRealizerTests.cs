using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Core.Assets;
using PulseStack.Core.Chat;
using PulseStack.Core.Runtime.Realization;
using PulseStack.Tests.Chat;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization;

public sealed class ModelRealizerTests
{
    [Fact]
    public void ModelRealizer_ShouldResolveFactoryFromAssetProvider()
    {
        var factory = new ChatClientFactoryRegistryTests.FakeChatClientFactory();
        var realizer = new ModelRealizer(CreateRegistry("TestProvider", factory));

        realizer.Realize(CreateAsset("TestProvider", "test-model"));

        factory.CreatedModel.Should().Be("test-model");
    }

    [Fact]
    public void ModelRealizer_ShouldCreateClientUsingAssetModel()
    {
        var factory = new ChatClientFactoryRegistryTests.FakeChatClientFactory();
        var realizer = new ModelRealizer(CreateRegistry("TestProvider", factory));

        var client = realizer.Realize(CreateAsset("TestProvider", "test-model"));

        client.Should().NotBeNull();
        factory.CreatedModel.Should().Be("test-model");
    }

    [Fact]
    public void ModelRealizer_ShouldRejectUnknownProvider()
    {
        var realizer = new ModelRealizer(new ChatClientFactoryRegistry([]));

        var action = () => realizer.Realize(CreateAsset("UnknownProvider", "test-model"));

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*UnknownProvider*");
    }

    private static ModelAsset CreateAsset(string provider, string model)
    {
        var options = new ModelAssetOptions(provider, model);
        return new ModelAssetFactory(new TestModelCatalog(options)).Create(options);
    }

    private static IChatClientFactoryRegistry CreateRegistry(
        string provider,
        IChatClientFactory factory)
        => new ChatClientFactoryRegistry(
        [
            new ChatClientFactoryRegistration(provider, factory)
        ]);

    private sealed class TestModelCatalog(params ModelAssetOptions[] models) : IModelCatalog
    {
        public IReadOnlyCollection<ProviderModelDescriptor> GetModels()
            => models.Select(model => new ProviderModelDescriptor(model.Provider, model.Model)).ToArray();

        public bool Contains(string provider, string model)
            => models.Any(candidate =>
                string.Equals(candidate.Provider, provider, StringComparison.OrdinalIgnoreCase)
                && string.Equals(candidate.Model, model, StringComparison.OrdinalIgnoreCase));
    }
}
