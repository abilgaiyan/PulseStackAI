using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Chat;
using PulseStack.Abstractions.Models;
using PulseStack.Abstractions.Policies;
using PulseStack.Abstractions.Providers;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Agents.DependencyInjection;
using PulseStack.Core.Assets;
using PulseStack.Core.DependencyInjection;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization;

public sealed class PolicyAgentComposerIsolationTests
{
    [Fact]
    public async Task ComposeAsync_Should_Bind_Only_Referenced_Policy()
    {
        var modelAsset = CreateModelAsset();
        var referencedAsset = CreatePolicyAsset("Approved Policy");
        var unreferencedAsset = CreatePolicyAsset("Unreferenced Policy");
        var referencedPolicy = new StubRuntimePolicy("approved-policy");
        var unreferencedPolicy = new StubRuntimePolicy("unreferenced-policy");
        var bindingResolver = new RecordingPolicyBindingResolver(
            new Dictionary<AssetId, IRuntimePolicy>
            {
                [referencedAsset.Id] = referencedPolicy,
                [unreferencedAsset.Id] = unreferencedPolicy
            });

        var services = new ServiceCollection();
        services.AddSingleton<IProviderResolver, StubProviderResolver>();
        services.AddSingleton<IAsset>(modelAsset);
        services.AddSingleton<IAsset>(referencedAsset);
        services.AddSingleton<IAsset>(unreferencedAsset);
        services.AddSingleton<IPolicyBindingResolver>(bindingResolver);
        services.AddPulseStack();
        services.AddPulseStackAgents();

        await using var provider = services.BuildServiceProvider();
        var composer = provider.GetRequiredService<IAgentComposer>();

        var definition = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "PolicyAgent",
                Goal = "Use only explicitly referenced policies",
                Role = "Governed worker",
                Model = Reference(modelAsset),
                Policies = [Reference(referencedAsset)]
            });

        var agent = await composer.ComposeAsync(definition);

        agent.Should().NotBeNull();
        bindingResolver.ResolvedAssets.Should().ContainSingle()
            .Which.Should().Be(referencedAsset.Id);
        bindingResolver.ResolvedAssets.Should().NotContain(unreferencedAsset.Id);
    }

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static ModelAsset CreateModelAsset()
    {
        var catalog = new StubModelCatalog();
        var factory = new ModelAssetFactory(catalog);

        return factory.Create(
            new ModelAssetOptions(
                "Stub",
                "stub-model"));
    }

    private static PolicyAsset CreatePolicyAsset(string name) =>
        new PolicyAssetFactory().Create(
            new PolicyAssetOptions
            {
                Name = name,
                Description = "Policy used by Agent realization tests.",
                Tags = ["tests"]
            });

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

    private sealed class StubRuntimePolicy(string name) : IRuntimePolicy
    {
        public string Name { get; } = name;
    }

    private sealed class RecordingPolicyBindingResolver(
        IReadOnlyDictionary<AssetId, IRuntimePolicy> policies)
        : IPolicyBindingResolver
    {
        public List<AssetId> ResolvedAssets { get; } = [];

        public IRuntimePolicy Resolve(PolicyAsset asset)
        {
            ResolvedAssets.Add(asset.Id);

            if (!policies.TryGetValue(asset.Id, out var policy))
            {
                throw new InvalidOperationException(
                    $"Unexpected Policy Asset '{asset.Urn.Value}'.");
            }

            return policy;
        }
    }
}
