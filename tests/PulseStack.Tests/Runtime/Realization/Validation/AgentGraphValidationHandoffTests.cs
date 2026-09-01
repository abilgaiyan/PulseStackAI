using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Models;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Providers;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Validation;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Core.Assets;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Validation;

public sealed class AgentGraphValidationHandoffTests
{
    [Fact]
    public async Task ReconstructedValidAgent_ShouldPassGraphValidationAndEnterAgentComposer()
    {
        var model = CreateModelAsset();
        var source = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "Persisted Agent",
                Goal = "Prove persistence-to-realization handoff.",
                Role = "Validation fixture",
                Model = Reference(model)
            });

        var services = new ServiceCollection();
        services.AddSingleton<IAsset>(model);
        services.AddSingleton<IProviderResolver, StubProviderResolver>();
        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var mapper = scope.ServiceProvider.GetRequiredService<IAIAssetDocumentMapper>();
        var validator = scope.ServiceProvider.GetRequiredService<IAgentGraphValidator>();
        var composer = scope.ServiceProvider.GetRequiredService<IAgentComposer>();

        var document = mapper.ToDocument(source);
        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<AgentDefinition>().Subject;

        var validation = await validator.ValidateAsync(reconstructed);

        validation.IsValid.Should().BeTrue();
        validation.Errors.Should().BeEmpty();

        var runtimeAgent = await composer.ComposeAsync(reconstructed);

        runtimeAgent.Should().NotBeNull();
        runtimeAgent.Name.Should().Be("Persisted Agent");
    }

    private static AssetReference Reference(IAsset asset)
        => new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static ModelAsset CreateModelAsset()
    {
        var catalog = new StubModelCatalog();
        return new ModelAssetFactory(catalog).Create(
            new ModelAssetOptions("Stub", "stub-model"));
    }

    private sealed class StubModelCatalog : IModelCatalog
    {
        public IReadOnlyCollection<ProviderModelDescriptor> GetModels()
            => [new ProviderModelDescriptor("Stub", "stub-model")];

        public bool Contains(string provider, string model)
            => string.Equals(provider, "Stub", StringComparison.Ordinal)
               && string.Equals(model, "stub-model", StringComparison.Ordinal);
    }

    private sealed class StubProviderResolver : IProviderResolver
    {
        private readonly IChatClientFactory factory = new StubChatClientFactory();

        public IChatClientFactory Resolve(string provider)
        {
            if (!string.Equals(provider, "Stub", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected provider '{provider}'.");
            }

            return factory;
        }
    }

    private sealed class StubChatClientFactory : IChatClientFactory
    {
        private readonly IChatClient client = new StubChatClient();

        public IChatClient Create(string model)
        {
            if (!string.Equals(model, "stub-model", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Unexpected model '{model}'.");
            }

            return client;
        }
    }

    private sealed class StubChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => null;

        public void Dispose()
        {
        }
    }
}
