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
    public async Task ResolveAsync_ShouldReturnExactVersion_WhenMultipleVersionsShareId()
    {
        var id = AssetId.New();
        var version1 = CreateAsset(id, "urn:pulsestack:test:asset", new AssetVersion("1.0.0"));
        var version2 = CreateAsset(id, "urn:pulsestack:test:asset", new AssetVersion("2.0.0"));
        var resolver = new InMemoryAssetResolver([version1, version2]);

        var resolved1 = await resolver.ResolveAsync(Reference(version1));
        var resolved2 = await resolver.ResolveAsync(Reference(version2));

        resolved1.Should().BeSameAs(version1);
        resolved2.Should().BeSameAs(version2);
    }

    [Fact]
    public async Task ResolveAsync_ShouldPermitSameIdAndVersionAcrossDifferentTypes()
    {
        var id = AssetId.New();
        var version = new AssetVersion("1.0.0");
        var model = CreateAsset(id, "urn:pulsestack:test:model", version, AssetType.Model);
        var prompt = CreateAsset(id, "urn:pulsestack:test:prompt", version, AssetType.Prompt);
        var resolver = new InMemoryAssetResolver([model, prompt]);

        var resolvedModel = await resolver.ResolveAsync(Reference(model));
        var resolvedPrompt = await resolver.ResolveAsync(Reference(prompt));

        resolvedModel.Should().BeSameAs(model);
        resolvedPrompt.Should().BeSameAs(prompt);
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNull_WhenDefinitionKeyMatchesButUrnDoesNot()
    {
        var asset = CreateAsset();
        var resolver = new InMemoryAssetResolver([asset]);
        var reference = new AssetReference(
            asset.Type,
            asset.Id,
            new AssetUrn("urn:pulsestack:test:conflict"),
            asset.Version);

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
    public void Constructor_ShouldRejectDuplicateDefinitionKey()
    {
        var first = CreateAsset();
        var second = CreateAsset(
            first.Id,
            "urn:pulsestack:test:second",
            first.Version,
            first.Type);

        var action = () => new InMemoryAssetResolver([first, second]);

        action.Should().Throw<ArgumentException>()
            .WithMessage($"*{first.Id}*");
    }

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static TestAsset CreateAsset(
        AssetId? id = null,
        string urn = "urn:pulsestack:test:asset",
        AssetVersion? version = null,
        AssetType type = AssetType.Model)
        => new(
            type,
            id ?? AssetId.New(),
            new AssetUrn(urn),
            version ?? AssetVersion.Initial);

    private sealed record TestAsset : Asset
    {
        [SetsRequiredMembers]
        public TestAsset(
            AssetType type,
            AssetId id,
            AssetUrn urn,
            AssetVersion version)
            : base(type)
        {
            Id = id;
            Urn = urn;
            Version = version;
            Metadata = new AssetMetadata { Name = "Test Asset", Tags = [] };
            Lifecycle = AssetLifecycle.Draft;
        }
    }
}
