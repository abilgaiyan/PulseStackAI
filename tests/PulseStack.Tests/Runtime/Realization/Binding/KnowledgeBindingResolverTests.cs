using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Knowledge;
using PulseStack.Abstractions.Runtime.Realization.Binding;
using PulseStack.Core.Assets;
using PulseStack.Core.Runtime.Realization.Binding;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Binding;

public sealed class KnowledgeBindingResolverTests
{
    [Fact]
    public void Resolve_ShouldReturnBoundKnowledgeSource()
    {
        var asset = CreateKnowledgeAsset();
        var source = new StubKnowledgeSource("customer-source");
        var resolver = new KnowledgeBindingResolver(
            [new KnowledgeBindingRegistration(
                new AssetReference(asset.Type, asset.Id, asset.Urn, asset.Version),
                source.Name)],
            [source]);

        resolver.Resolve(asset).Should().BeSameAs(source);
    }

    [Fact]
    public void Resolve_ShouldRejectUnboundKnowledgeAsset()
    {
        var asset = CreateKnowledgeAsset();
        var resolver = new KnowledgeBindingResolver([], []);

        var action = () => resolver.Resolve(asset);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not bound*");
    }

    private static KnowledgeAsset CreateKnowledgeAsset() =>
        new KnowledgeAssetFactory().Create(
            new KnowledgeAssetOptions
            {
                Name = "Customer Knowledge",
                Description = "Customer reference knowledge."
            });

    private sealed class StubKnowledgeSource(string name) : IKnowledgeSource
    {
        public string Name { get; } = name;

        public Task<KnowledgeResult> RetrieveAsync(
            KnowledgeQuery query,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new KnowledgeResult
            {
                Items = ["result"]
            });
    }
}
