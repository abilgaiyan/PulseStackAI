using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Abstractions.Providers;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Core.Assets;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization;

public sealed class AgentComposerTests
{
    [Fact]
    public async Task ComposeAsync_Should_Resolve_Model_And_Create_Runtime_Agent()
    {
        var modelAsset = CreateModelAsset();

        var services = new ServiceCollection();

        services.AddSingleton<IProviderResolver, StubProviderResolver>();
        services.AddScoped<IAssetResolver>(_ =>
            new StubAssetResolver(modelAsset));

        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider();

        var composer = provider.GetRequiredService<IAgentComposer>();

        var definition = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "TestAgent",
                Goal = "Test model realization",
                Role = "Test worker",
                Model = new AssetReference(
                    modelAsset.Id,
                    modelAsset.Urn)
            });

        var agent = await composer.ComposeAsync(definition);

        agent.Should().NotBeNull();
        agent.Name.Should().Be("TestAgent");
    }

    [Fact]
    public async Task ComposeAsync_Should_Reject_Agent_Without_Model()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProviderResolver, StubProviderResolver>();
        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider();

        var composer = provider.GetRequiredService<IAgentComposer>();

        var definition = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "TestAgent",
                Goal = "Test validation",
                Role = "Test worker"
            });

        var action = () => composer.ComposeAsync(definition);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*requires a Model Asset reference*");
    }

    private static ModelAsset CreateModelAsset()
    {
        var catalog = new StubModelCatalog();
        var factory = new ModelAssetFactory(catalog);

        return factory.Create(
            new ModelAssetOptions(
                "Stub",
                "stub-model"));
    }

    private sealed class StubAssetResolver : IAssetResolver
    {
        private readonly ModelAsset _modelAsset;

        public StubAssetResolver(ModelAsset modelAsset)
        {
            _modelAsset = modelAsset;
        }

        public ValueTask<IAsset?> ResolveAsync(
            AssetReference reference,
            CancellationToken cancellationToken = default)
        {
            if (reference.Id == _modelAsset.Id)
            {
                return ValueTask.FromResult<IAsset?>(_modelAsset);
            }

            return ValueTask.FromResult<IAsset?>(null);
        }
    }

    private sealed class StubModelCatalog : IModelCatalog
    {
        public IReadOnlyCollection<ProviderModelDescriptor> GetModels() =>
            [new ProviderModelDescriptor("Stub", "stub-model")];

        public bool Contains(string provider, string model) =>
            provider == "Stub" && model == "stub-model";
    }

    private sealed class StubProviderResolver : IProviderResolver
    {
        private readonly IChatClientFactory _factory =
            new StubChatClientFactory();

        public IChatClientFactory Resolve(string provider)
        {
            if (provider != "Stub")
            {
                throw new InvalidOperationException(
                    $"Unexpected provider '{provider}'.");
            }

            return _factory;
        }
    }

    private sealed class StubChatClientFactory : IChatClientFactory
    {
        public IChatClient Create(string model)
        {
            if (model != "stub-model")
            {
                throw new InvalidOperationException(
                    $"Unexpected model '{model}'.");
            }

            return new StubChatClient();
        }
    }

    private sealed class StubChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(
            Type serviceType,
            object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
