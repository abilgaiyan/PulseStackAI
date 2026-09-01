using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Memory;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Core.Assets;
using PulseStack.Core.Runtime.Realization.Binding;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Binding;

public sealed class MemoryBindingResolverTests
{
    [Fact]
    public void Resolve_ShouldCreateFreshMemory_FromBoundFactory()
    {
        var asset = CreateMemoryAsset();
        var factory = new StubMemoryFactory("conversation");
        var resolver = new MemoryBindingResolver(
            [new MemoryBindingRegistration(Reference(asset), factory.Name)],
            [factory]);

        var first = resolver.Resolve(asset);
        var second = resolver.Resolve(asset);

        first.Should().NotBeSameAs(second);
        factory.CreateCount.Should().Be(2);
    }

    [Fact]
    public void Resolve_ShouldRejectUnboundMemoryAsset()
    {
        var asset = CreateMemoryAsset();
        var resolver = new MemoryBindingResolver([], []);

        var action = () => resolver.Resolve(asset);

        action.Should().Throw<InvalidOperationException>().WithMessage("*not bound*");
    }

    [Fact]
    public void Resolve_ShouldRejectBindingWithWrongAssetType()
    {
        var asset = CreateMemoryAsset();
        var factory = new StubMemoryFactory("conversation");
        var resolver = new MemoryBindingResolver(
            [new MemoryBindingRegistration(
                new AssetReference(AssetType.Policy, asset.Id, asset.Urn, asset.Version),
                factory.Name)],
            [factory]);

        var action = () => resolver.Resolve(asset);

        action.Should().Throw<InvalidOperationException>().WithMessage("*not bound*");
    }

    [Fact]
    public void Resolve_ShouldRejectBindingWithWrongAssetVersion()
    {
        var asset = CreateMemoryAsset();
        var factory = new StubMemoryFactory("conversation");
        var resolver = new MemoryBindingResolver(
            [new MemoryBindingRegistration(
                new AssetReference(asset.Type, asset.Id, asset.Urn, new AssetVersion("2.0.0")),
                factory.Name)],
            [factory]);

        var action = () => resolver.Resolve(asset);

        action.Should().Throw<InvalidOperationException>().WithMessage("*not bound*");
    }

    [Fact]
    public void Resolve_ShouldRejectBindingWithWrongAssetUrn()
    {
        var asset = CreateMemoryAsset();
        var factory = new StubMemoryFactory("conversation");
        var resolver = new MemoryBindingResolver(
            [new MemoryBindingRegistration(
                new AssetReference(asset.Type, asset.Id, new AssetUrn("urn:pulsestack:memory:other"), asset.Version),
                factory.Name)],
            [factory]);

        var action = () => resolver.Resolve(asset);

        action.Should().Throw<InvalidOperationException>().WithMessage("*not bound*");
    }

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static MemoryAsset CreateMemoryAsset() =>
        new MemoryAssetFactory().Create(
            new MemoryAssetOptions
            {
                Name = "Conversation Memory",
                Description = "Retains conversational context."
            });

    private sealed class StubMemoryFactory(string name) : IConversationMemoryFactory
    {
        public int CreateCount { get; private set; }
        public string Name { get; } = name;

        public IConversationMemory Create()
        {
            CreateCount++;
            return new StubMemory();
        }
    }

    private sealed class StubMemory : IConversationMemory
    {
        private readonly List<Microsoft.Extensions.AI.ChatMessage> _messages = [];
        public IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> Messages => _messages;
        public void Add(Microsoft.Extensions.AI.ChatMessage message) => _messages.Add(message);
        public void Clear() => _messages.Clear();
    }
}
