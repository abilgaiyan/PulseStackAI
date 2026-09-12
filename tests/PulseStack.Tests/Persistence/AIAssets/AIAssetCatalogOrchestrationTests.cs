using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.Persistence.AIAssets.Catalog;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetCatalogOrchestrationTests
{
    [Fact]
    public async Task ExactResolution_WhenCatalogIsAbsent_ShouldNotLoad()
    {
        var fixture = new Fixture();

        var result = await fixture.Resolver.ResolveAsync(fixture.Key);

        result.Should().BeOfType<AIAssetResolutionResult.DefinitionNotPublished>();
        fixture.Events.Should().Equal("catalog:exact");
        fixture.Loader.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExactResolution_WhenPublished_ShouldLoadOnceAndReturnLoaderAsset()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(fixture.Record);
        fixture.Loader.Result = new AIAssetLoadResult.Loaded(fixture.Asset);

        var result = await fixture.Resolver.ResolveAsync(fixture.Key);

        var resolved = result.Should().BeOfType<AIAssetResolutionResult.Resolved>().Subject;
        resolved.Asset.Should().BeSameAs(fixture.Asset);
        fixture.Events.Should().Equal("catalog:exact", "loader");
        fixture.Loader.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task ReferenceResolution_WhenUrnDisagrees_ShouldNotLoad()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(fixture.Record);
        var reference = new AssetReference(
            fixture.Key.Type,
            fixture.Key.Id,
            new AssetUrn("urn:pulsestack:prompt:other"),
            fixture.Key.Version);

        var result = await fixture.Resolver.ResolveAsync(reference);

        result.Should().BeOfType<AIAssetResolutionResult.ReferenceMismatch>();
        fixture.Events.Should().Equal("catalog:exact");
        fixture.Loader.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task UrnVersionResolution_WhenLineageIsAbsent_ShouldStopBeforeExactLookup()
    {
        var fixture = new Fixture();

        var result = await fixture.Resolver.ResolveAsync(fixture.Urn, fixture.Key.Version);

        result.Should().BeOfType<AIAssetResolutionResult.LineageNotPublished>();
        fixture.Events.Should().Equal("catalog:lineage");
    }

    [Fact]
    public async Task UrnVersionResolution_WhenVersionIsAbsent_ShouldStopBeforeExactLookup()
    {
        var fixture = new Fixture();
        fixture.Catalog.LineageResult = new CatalogLineageLookupResult.Found(
            new CatalogLineage(fixture.Key.Type, fixture.Key.Id, fixture.Urn, [new AssetVersion("2.0")]));

        var result = await fixture.Resolver.ResolveAsync(fixture.Urn, fixture.Key.Version);

        result.Should().BeOfType<AIAssetResolutionResult.DefinitionNotPublished>();
        fixture.Events.Should().Equal("catalog:lineage");
    }

    [Fact]
    public async Task UrnVersionResolution_WhenLineageClaimsMissingExactRecord_ShouldBeInconsistent()
    {
        var fixture = new Fixture();
        fixture.Catalog.LineageResult = new CatalogLineageLookupResult.Found(fixture.Lineage);

        var act = async () => await fixture.Resolver.ResolveAsync(fixture.Urn, fixture.Key.Version);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.InconsistentState);
        fixture.Events.Should().Equal("catalog:lineage", "catalog:exact");
        fixture.Loader.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Resolution_WhenPublishedDefinitionCannotBeLoaded_ShouldUseBoundaryFailure()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(fixture.Record);

        var act = async () => await fixture.Resolver.ResolveAsync(fixture.Key);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogBoundaryException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogBoundaryFailureCategory.PublishedDefinitionUnavailable);
        fixture.Events.Should().Equal("catalog:exact", "loader");
    }

    [Fact]
    public async Task Resolution_WhenLoadedUrnDisagrees_ShouldUseBoundaryFailure()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(fixture.Record);
        fixture.Loader.Result = new AIAssetLoadResult.Loaded(
            new TestAsset(fixture.Key, new AssetUrn("urn:pulsestack:prompt:other")));

        var act = async () => await fixture.Resolver.ResolveAsync(fixture.Key);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogBoundaryException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogBoundaryFailureCategory.CatalogAssetIdentityMismatch);
    }

    [Fact]
    public async Task Publisher_WhenDefinitionIsNotStored_ShouldNotPublish()
    {
        var fixture = new Fixture();

        var result = await fixture.Publisher.PublishAsync(fixture.Key);

        result.Should().Be(AIAssetPublicationResult.DefinitionNotStored);
        fixture.Events.Should().Equal("catalog:exact", "loader");
        fixture.Catalog.PublishCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(CatalogPublicationResult.Created, AIAssetPublicationResult.Published)]
    [InlineData(CatalogPublicationResult.AlreadyPresent, AIAssetPublicationResult.AlreadyPublished)]
    [InlineData(CatalogPublicationResult.Conflict, AIAssetPublicationResult.IdentityConflict)]
    public async Task Publisher_WhenCatalogIsAbsent_ShouldMapSingleProviderPublicationOutcome(
        CatalogPublicationResult providerResult,
        AIAssetPublicationResult expected)
    {
        var fixture = new Fixture();
        fixture.Loader.Result = new AIAssetLoadResult.Loaded(fixture.Asset);
        fixture.Catalog.PublicationResult = providerResult;

        var result = await fixture.Publisher.PublishAsync(fixture.Key);

        result.Should().Be(expected);
        fixture.Events.Should().Equal("catalog:exact", "loader", "catalog:publish");
        fixture.Loader.CallCount.Should().Be(1);
        fixture.Catalog.PublishCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Publisher_WhenAlreadyPublished_ShouldVerifyLoaderAndSkipRawPublish()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(fixture.Record);
        fixture.Loader.Result = new AIAssetLoadResult.Loaded(fixture.Asset);

        var result = await fixture.Publisher.PublishAsync(fixture.Key);

        result.Should().Be(AIAssetPublicationResult.AlreadyPublished);
        fixture.Events.Should().Equal("catalog:exact", "loader");
        fixture.Catalog.PublishCallCount.Should().Be(0);
    }

    [Fact]
    public async Task CatalogProviderOperationalFailure_ShouldBeClassifiedAsProviderFailure()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactException = new IOException("provider failed");

        var act = async () => await fixture.Resolver.ResolveAsync(fixture.Key);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.ProviderFailure);
        exception.InnerException.Should().BeOfType<IOException>();
        fixture.Loader.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task LoaderFailure_ShouldPreserveMS0097ExceptionIdentity()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactResult = new ExactCatalogLookupResult.Found(fixture.Record);
        var expected = new AIAssetStorageException(
            AIAssetStorageFailureCategory.DocumentValidation,
            "validation failed");
        fixture.Loader.Exception = expected;

        var act = async () => await fixture.Resolver.ResolveAsync(fixture.Key);

        var thrown = (await act.Should().ThrowAsync<AIAssetStorageException>()).Which;
        thrown.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task PreCanceledRequest_ShouldMakeNoDependencyCalls()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var act = async () => await fixture.Publisher.PublishAsync(fixture.Key, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        fixture.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task IncidentalProviderCancellation_ShouldBecomeProviderFailure()
    {
        var fixture = new Fixture();
        fixture.Catalog.ExactException = new OperationCanceledException();

        var act = async () => await fixture.Resolver.ResolveAsync(fixture.Key);

        var exception = (await act.Should().ThrowAsync<AIAssetCatalogException>()).Which;
        exception.Category.Should().Be(AIAssetCatalogFailureCategory.ProviderFailure);
    }

    [Fact]
    public async Task CallerCancellationRaisedByProvider_ShouldRemainCancellation()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Catalog.BeforeExactException = () => cancellation.Cancel();
        fixture.Catalog.ExactException = new OperationCanceledException(cancellation.Token);

        var act = async () => await fixture.Resolver.ResolveAsync(fixture.Key, cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        fixture.Loader.CallCount.Should().Be(0);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Key = new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), new AssetVersion("1.0"));
            Urn = new AssetUrn("urn:pulsestack:prompt:example");
            Record = new CatalogRecord(Key, Urn);
            Lineage = new CatalogLineage(Key.Type, Key.Id, Urn, [Key.Version]);
            Asset = new TestAsset(Key, Urn);
            Catalog = new TestCatalog(Events);
            Loader = new TestLoader(Events);
            Resolver = new PersistentAIAssetResolver(Catalog, Loader);
            Publisher = new AIAssetPublisher(Catalog, Loader);
        }

        public List<string> Events { get; } = [];
        public AssetDefinitionKey Key { get; }
        public AssetUrn Urn { get; }
        public CatalogRecord Record { get; }
        public CatalogLineage Lineage { get; }
        public TestAsset Asset { get; }
        public TestCatalog Catalog { get; }
        public TestLoader Loader { get; }
        public PersistentAIAssetResolver Resolver { get; }
        public AIAssetPublisher Publisher { get; }
    }

    private sealed class TestCatalog(List<string> events) : IAIAssetCatalogProvider
    {
        public ExactCatalogLookupResult ExactResult { get; set; } = new ExactCatalogLookupResult.NotFound();
        public CatalogLineageLookupResult LineageResult { get; set; } = new CatalogLineageLookupResult.NotFound();
        public CatalogPublicationResult PublicationResult { get; set; } = CatalogPublicationResult.Created;
        public Exception? ExactException { get; set; }
        public Exception? LineageException { get; set; }
        public Exception? PublishException { get; set; }
        public Action? BeforeExactException { get; set; }
        public int PublishCallCount { get; private set; }

        public ValueTask<ExactCatalogLookupResult> FindExactAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            events.Add("catalog:exact");
            if (ExactException is not null)
            {
                BeforeExactException?.Invoke();
                return ValueTask.FromException<ExactCatalogLookupResult>(ExactException);
            }

            return ValueTask.FromResult(ExactResult);
        }

        public ValueTask<CatalogLineageLookupResult> FindLineageAsync(
            AssetUrn urn,
            CancellationToken cancellationToken = default)
        {
            events.Add("catalog:lineage");
            return LineageException is null
                ? ValueTask.FromResult(LineageResult)
                : ValueTask.FromException<CatalogLineageLookupResult>(LineageException);
        }

        public ValueTask<CatalogPublicationResult> PublishAsync(
            CatalogRecord record,
            CancellationToken cancellationToken = default)
        {
            events.Add("catalog:publish");
            PublishCallCount++;
            return PublishException is null
                ? ValueTask.FromResult(PublicationResult)
                : ValueTask.FromException<CatalogPublicationResult>(PublishException);
        }
    }

    private sealed class TestLoader(List<string> events) : IAIAssetLoader
    {
        public AIAssetLoadResult Result { get; set; } = new AIAssetLoadResult.NotFound();
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }

        public ValueTask<AIAssetLoadResult> LoadAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            events.Add("loader");
            CallCount++;
            return Exception is null
                ? ValueTask.FromResult(Result)
                : ValueTask.FromException<AIAssetLoadResult>(Exception);
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
