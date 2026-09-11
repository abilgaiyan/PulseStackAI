using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.Persistence.AIAssets.Catalog;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetCatalogOrchestrationConformanceTests
{
    [Fact]
    public async Task Publisher_WhenCatalogPresentButLoaderNotFound_ShouldThrowBoundaryFailureAndNeverPublish()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(fixture.Record);

        var act = async () => await fixture.Publisher.PublishAsync(fixture.Key);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogBoundaryException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogBoundaryFailureCategory.PublishedDefinitionUnavailable);
        fixture.Loader.Keys.Should().Equal(fixture.Key);
        fixture.Catalog.PublishedRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task Publisher_WhenCatalogPresentButLoadedUrnDiffers_ShouldThrowBoundaryFailureAndNeverPublish()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(fixture.Record);
        fixture.Loader.Result = new AIAssetLoadResult.Loaded(
            new TestAsset(fixture.Key, new AssetUrn("urn:pulsestack:prompt:other")));

        var act = async () => await fixture.Publisher.PublishAsync(fixture.Key);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogBoundaryException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogBoundaryFailureCategory.CatalogAssetIdentityMismatch);
        fixture.Loader.Keys.Should().Equal(fixture.Key);
        fixture.Catalog.PublishedRecords.Should().BeEmpty();
    }

    [Fact]
    public async Task UrnVersionResolution_WhenPublished_ShouldDeriveExactKeyForLookupAndLoader()
    {
        var fixture = new Fixture();
        fixture.Catalog.LineageResult = new CatalogLineageLookupResult.Found(fixture.Lineage);
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(fixture.Record);
        fixture.Loader.Result = new AIAssetLoadResult.Loaded(fixture.Asset);

        var result = await fixture.Resolver.ResolveAsync(fixture.Urn, fixture.Key.Version);

        result.Should().BeOfType<AIAssetResolutionResult.Resolved>()
            .Which.Asset.Should().BeSameAs(fixture.Asset);
        fixture.Catalog.LineageUrns.Should().Equal(fixture.Urn);
        fixture.Catalog.ExactKeys.Should().Equal(fixture.Key);
        fixture.Loader.Keys.Should().Equal(fixture.Key);
        fixture.Events.Should().Equal("catalog:lineage", "catalog:exact", "loader");
    }

    [Fact]
    public async Task DiscoverLineage_ShouldOnlyLookupLineageAndReturnProviderSnapshot()
    {
        var fixture = new Fixture();
        fixture.Catalog.LineageResult = new CatalogLineageLookupResult.Found(fixture.Lineage);

        var result = await fixture.Resolver.DiscoverLineageAsync(fixture.Urn);

        var found = result.Should().BeOfType<CatalogLineageLookupResult.Found>().Subject;
        found.Lineage.Should().BeSameAs(fixture.Lineage);
        fixture.Catalog.LineageUrns.Should().Equal(fixture.Urn);
        fixture.Catalog.ExactKeys.Should().BeEmpty();
        fixture.Loader.Keys.Should().BeEmpty();
        fixture.Events.Should().Equal("catalog:lineage");
    }

    [Fact]
    public async Task ExactResolution_ShouldPropagateExactKeyAndCallerToken()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(fixture.Record);
        fixture.Loader.Result = new AIAssetLoadResult.Loaded(fixture.Asset);
        using var cancellation = new CancellationTokenSource();

        await fixture.Resolver.ResolveAsync(fixture.Key, cancellation.Token);

        fixture.Catalog.ExactKeys.Should().Equal(fixture.Key);
        fixture.Loader.Keys.Should().Equal(fixture.Key);
        fixture.Catalog.ExactTokens.Should().ContainSingle().Which.Should().Be(cancellation.Token);
        fixture.Loader.Tokens.Should().ContainSingle().Which.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task ReferenceResolution_ShouldPropagateDerivedKeyAndCallerToken()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(fixture.Record);
        fixture.Loader.Result = new AIAssetLoadResult.Loaded(fixture.Asset);
        var reference = new AssetReference(fixture.Key.Type, fixture.Key.Id, fixture.Urn, fixture.Key.Version);
        using var cancellation = new CancellationTokenSource();

        await fixture.Resolver.ResolveAsync(reference, cancellation.Token);

        fixture.Catalog.ExactKeys.Should().Equal(fixture.Key);
        fixture.Loader.Keys.Should().Equal(fixture.Key);
        fixture.Catalog.ExactTokens.Should().ContainSingle().Which.Should().Be(cancellation.Token);
        fixture.Loader.Tokens.Should().ContainSingle().Which.Should().Be(cancellation.Token);
    }

    [Fact]
    public async Task Publisher_ShouldPublishLoadedUrnForRequestedKeyAndPropagateCallerToken()
    {
        var fixture = new Fixture();
        fixture.Loader.Result = new AIAssetLoadResult.Loaded(fixture.Asset);
        using var cancellation = new CancellationTokenSource();

        var result = await fixture.Publisher.PublishAsync(fixture.Key, cancellation.Token);

        result.Should().Be(AIAssetPublicationResult.Published);
        fixture.Catalog.ExactKeys.Should().Equal(fixture.Key);
        fixture.Loader.Keys.Should().Equal(fixture.Key);
        fixture.Catalog.PublishedRecords.Should().ContainSingle();
        fixture.Catalog.PublishedRecords[0].DefinitionKey.Should().Be(fixture.Key);
        fixture.Catalog.PublishedRecords[0].Urn.Should().Be(fixture.Asset.Urn);
        fixture.Catalog.ExactTokens.Should().ContainSingle().Which.Should().Be(cancellation.Token);
        fixture.Loader.Tokens.Should().ContainSingle().Which.Should().Be(cancellation.Token);
        fixture.Catalog.PublishTokens.Should().ContainSingle().Which.Should().Be(cancellation.Token);
    }

    [Fact]
    public void Constructors_WhenDependencyIsNull_ShouldUseCompositionConfiguration()
    {
        var fixture = new Fixture();

        Action resolverCatalog = () => _ = new PersistentAIAssetResolver(null!, fixture.Loader);
        Action resolverLoader = () => _ = new PersistentAIAssetResolver(fixture.Catalog, null!);
        Action publisherCatalog = () => _ = new AIAssetPublisher(null!, fixture.Loader);
        Action publisherLoader = () => _ = new AIAssetPublisher(fixture.Catalog, null!);

        resolverCatalog.Should().Throw<AIAssetCatalogException>()
            .Which.Category.Should().Be(AIAssetCatalogFailureCategory.CompositionConfiguration);
        resolverLoader.Should().Throw<AIAssetCatalogException>()
            .Which.Category.Should().Be(AIAssetCatalogFailureCategory.CompositionConfiguration);
        publisherCatalog.Should().Throw<AIAssetCatalogException>()
            .Which.Category.Should().Be(AIAssetCatalogFailureCategory.CompositionConfiguration);
        publisherLoader.Should().Throw<AIAssetCatalogException>()
            .Which.Category.Should().Be(AIAssetCatalogFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public async Task ExactResolution_WhenProviderReturnsDifferentKey_ShouldBeInconsistentWithoutLoading()
    {
        var fixture = new Fixture();
        var otherKey = new AssetDefinitionKey(fixture.Key.Type, AssetId.New(), fixture.Key.Version);
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(new CatalogRecord(otherKey, fixture.Urn));

        var act = async () => await fixture.Resolver.ResolveAsync(fixture.Key);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.InconsistentState);
        fixture.Loader.Keys.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverLineage_WhenProviderReturnsDifferentUrn_ShouldBeInconsistent()
    {
        var fixture = new Fixture();
        fixture.Catalog.LineageResult = new CatalogLineageLookupResult.Found(
            new CatalogLineage(
                fixture.Key.Type,
                fixture.Key.Id,
                new AssetUrn("urn:pulsestack:prompt:other"),
                new[] { fixture.Key.Version }));

        var act = async () => await fixture.Resolver.DiscoverLineageAsync(fixture.Urn);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.InconsistentState);
        fixture.Catalog.ExactKeys.Should().BeEmpty();
        fixture.Loader.Keys.Should().BeEmpty();
    }

    [Fact]
    public async Task UrnVersionResolution_WhenExactRecordDisagreesWithLineageUrn_ShouldBeInconsistentWithoutLoading()
    {
        var fixture = new Fixture();
        fixture.Catalog.LineageResult = new CatalogLineageLookupResult.Found(fixture.Lineage);
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(
            new CatalogRecord(fixture.Key, new AssetUrn("urn:pulsestack:prompt:other")));

        var act = async () => await fixture.Resolver.ResolveAsync(fixture.Urn, fixture.Key.Version);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.InconsistentState);
        fixture.Loader.Keys.Should().BeEmpty();
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Key = new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), new AssetVersion("1.0"));
            Urn = new AssetUrn("urn:pulsestack:prompt:example");
            Record = new CatalogRecord(Key, Urn);
            Lineage = new CatalogLineage(Key.Type, Key.Id, Urn, new[] { Key.Version });
            Asset = new TestAsset(Key, Urn);
            Catalog = new CapturingCatalog(Events);
            Loader = new CapturingLoader(Events);
            Resolver = new PersistentAIAssetResolver(Catalog, Loader);
            Publisher = new AIAssetPublisher(Catalog, Loader);
        }

        public List<string> Events { get; } = [];
        public AssetDefinitionKey Key { get; }
        public AssetUrn Urn { get; }
        public CatalogRecord Record { get; }
        public CatalogLineage Lineage { get; }
        public TestAsset Asset { get; }
        public CapturingCatalog Catalog { get; }
        public CapturingLoader Loader { get; }
        public PersistentAIAssetResolver Resolver { get; }
        public AIAssetPublisher Publisher { get; }
    }

    private sealed class CapturingCatalog(List<string> events) : IAIAssetCatalogProvider
    {
        public ExactCatalogLookupResult ExactResult { get; set; } = new ExactCatalogLookupResult.NotFound();
        public CatalogLineageLookupResult LineageResult { get; set; } = new CatalogLineageLookupResult.NotFound();
        public CatalogPublicationResult PublicationResult { get; set; } = CatalogPublicationResult.Created;
        public List<AssetDefinitionKey> ExactKeys { get; } = [];
        public List<AssetUrn> LineageUrns { get; } = [];
        public List<CatalogRecord> PublishedRecords { get; } = [];
        public List<CancellationToken> ExactTokens { get; } = [];
        public List<CancellationToken> LineageTokens { get; } = [];
        public List<CancellationToken> PublishTokens { get; } = [];

        public ValueTask<ExactCatalogLookupResult> FindExactAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            events.Add("catalog:exact");
            ExactKeys.Add(key);
            ExactTokens.Add(cancellationToken);
            return ValueTask.FromResult(ExactResult);
        }

        public ValueTask<CatalogLineageLookupResult> FindLineageAsync(
            AssetUrn urn,
            CancellationToken cancellationToken = default)
        {
            events.Add("catalog:lineage");
            LineageUrns.Add(urn);
            LineageTokens.Add(cancellationToken);
            return ValueTask.FromResult(LineageResult);
        }

        public ValueTask<CatalogPublicationResult> PublishAsync(
            CatalogRecord record,
            CancellationToken cancellationToken = default)
        {
            events.Add("catalog:publish");
            PublishedRecords.Add(record);
            PublishTokens.Add(cancellationToken);
            return ValueTask.FromResult(PublicationResult);
        }
    }

    private sealed class CapturingLoader(List<string> events) : IAIAssetLoader
    {
        public AIAssetLoadResult Result { get; set; } = new AIAssetLoadResult.NotFound();
        public List<AssetDefinitionKey> Keys { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];

        public ValueTask<AIAssetLoadResult> LoadAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            events.Add("loader");
            Keys.Add(key);
            Tokens.Add(cancellationToken);
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class TestAsset(AssetDefinitionKey key, AssetUrn urn) : IAsset
    {
        public AssetId Id => key.Id;
        public AssetUrn Urn => urn;
        public AssetVersion Version => key.Version;
        public AssetMetadata Metadata { get; } = new() { Name = "Test" };
        public AssetType Type => key.Type;
        public AssetLifecycle Lifecycle => AssetLifecycle.Published;
        public IReadOnlyCollection<AssetReference> References { get; } = Array.Empty<AssetReference>();
        public IReadOnlyCollection<AssetDependency> Dependencies { get; } = Array.Empty<AssetDependency>();
    }
}
