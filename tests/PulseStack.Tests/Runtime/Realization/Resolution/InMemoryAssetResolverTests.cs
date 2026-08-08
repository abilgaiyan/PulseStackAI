using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Runtime.Realization.Resolution;

namespace PulseStack.Tests.Runtime.Realization.Resolution;

public sealed class InMemoryAssetResolverTests
{
    [Fact]
    public async Task ResolveAsync_ShouldReturnAsset_WhenReferenceMatchesIdAndUrn()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);
        var reference = new AssetReference(asset.Id, asset.Urn);

        var resolved = await resolver.ResolveAsync(reference);

        resolved.Should().BeSameAs(asset);
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenIdDoesNotExist()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);
        var reference = new AssetReference(AssetId.New(), asset.Urn);

        var resolved = await resolver.ResolveAsync(reference);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenUrnDoesNotMatch()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);
        var reference = new AssetReference(
            asset.Id,
            new AssetUrn("urn:pulsestack:test:other"));

        var resolved = await resolver.ResolveAsync(reference);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenReferenceIsInvalid()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);

        var resolved = await resolver.ResolveAsync(
            new AssetReference(AssetId.Empty, new AssetUrn(string.Empty)));

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldHonorCancellation()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var action = () => resolver.ResolveAsync(
            new AssetReference(asset.Id, asset.Urn),
            cancellationTokenSource.Token).AsTask();

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public void Constructor_ShouldRejectDuplicateAssetIds()
    {
        var first = CreateAsset();
        var second = CreateAsset(first.Id, "urn:pulsestack:test:second");

        var action = () => new InMemoryAssetResolver([first, second]);

        action.Should().Throw<ArgumentException>()
            .WithMessage($"*{first.Id}*");
    }

    private static TestAsset CreateAsset(
        AssetId? id = null,
        string urn = "urn:pulsestack:test:asset")
        => new(
            id ?? AssetId.New(),
            new AssetUrn(urn));

    private sealed record TestAsset : Asset
    {
        public TestAsset(AssetId id, AssetUrn urn)
        {
            Id = id;
            Urn = urn;
            Version = AssetVersion.Initial;
            Metadata = new AssetMetadata { Name = "Test Asset", Tags = [] };
            Lifecycle = AssetLifecycle.Draft;
        }
    }
}
