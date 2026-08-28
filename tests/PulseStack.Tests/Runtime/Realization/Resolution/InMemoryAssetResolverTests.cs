using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Runtime.Realization.Resolution;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Resolution;

public sealed class InMemoryAssetResolverTests
{
    [Fact]
    public async Task ResolveAsync_ShouldReturnAsset_WhenExactReferenceMatches()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);
        var reference = Reference(asset);

        var resolved = await resolver.ResolveAsync(reference);

        resolved.Should().BeSameAs(asset);
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenIdDoesNotExist()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);
        var reference = new AssetReference(
            asset.Type,
            AssetId.New(),
            asset.Urn,
            asset.Version);

        var resolved = await resolver.ResolveAsync(reference);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenUrnDoesNotMatch()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);
        var reference = new AssetReference(
            asset.Type,
            asset.Id,
            new AssetUrn("urn:pulsestack:test:other"),
            asset.Version);

        var resolved = await resolver.ResolveAsync(reference);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenTypeDoesNotMatch()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);
        var reference = new AssetReference(
            AssetType.Prompt,
            asset.Id,
            asset.Urn,
            asset.Version);

        var resolved = await resolver.ResolveAsync(reference);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenVersionDoesNotMatch()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);
        var reference = new AssetReference(
            asset.Type,
            asset.Id,
            asset.Urn,
            new AssetVersion("2.0.0"));

        var resolved = await resolver.ResolveAsync(reference);

        resolved.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenReferenceIsInvalid()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);

        var resolved = await resolver.ResolveAsync(
            new AssetReference(
                (AssetType)999,
                AssetId.Empty,
                new AssetUrn(string.Empty),
                new AssetVersion(string.Empty)));

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
            Reference(asset),
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

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static TestAsset CreateAsset(
        AssetId? id = null,
        string urn = "urn:pulsestack:test:asset")
        => new(
            id ?? AssetId.New(),
            new AssetUrn(urn));

    private sealed record TestAsset : Asset
    {
        [SetsRequiredMembers]
        public TestAsset(AssetId id, AssetUrn urn)
            : base(AssetType.Model)
        {
            Id = id;
            Urn = urn;
            Version = AssetVersion.Initial;
            Metadata = new AssetMetadata { Name = "Test Asset", Tags = [] };
            Lifecycle = AssetLifecycle.Draft;
        }
    }
}
