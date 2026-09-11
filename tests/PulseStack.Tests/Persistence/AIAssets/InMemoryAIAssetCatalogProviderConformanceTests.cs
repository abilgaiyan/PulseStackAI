using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Core.Persistence.AIAssets.Catalog;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class InMemoryAIAssetCatalogProviderConformanceTests
    : AIAssetCatalogProviderConformanceTests
{
    protected override ValueTask<AIAssetCatalogProviderConformanceFixture> CreateFixtureAsync()
    {
        var catalogNamespace = new InMemoryAIAssetCatalogNamespace();

        var fixture = new AIAssetCatalogProviderConformanceFixture(
            createProvider: () => new InMemoryAIAssetCatalogProvider(catalogNamespace),
            capabilityProfile: new AIAssetCatalogCapabilityProfile(AIAssetAuthorityDurability.Transient),
            failureScenario: new AIAssetCatalogProviderFailureScenario(
                ObserveProviderFailureAsync,
                ObserveInconsistentStateAsync),
            tokenObservation: new AIAssetCatalogProviderTokenObservation(
                () => catalogNamespace.TestHooks.LastExactLookupToken,
                () => catalogNamespace.TestHooks.LastLineageLookupToken,
                () => catalogNamespace.TestHooks.LastPublicationToken));

        return ValueTask.FromResult(fixture);

        async ValueTask<Exception> ObserveProviderFailureAsync(IAIAssetCatalogProvider provider)
        {
            lock (catalogNamespace.SyncRoot)
            {
                catalogNamespace.TestHooks.NextProviderFailure = new IOException("Injected in-memory provider failure.");
            }

            try
            {
                await provider.FindExactAsync(CreateKey());
                return new InvalidOperationException("The injected provider failure was not observed.");
            }
            catch (Exception ex)
            {
                return ex;
            }
        }

        async ValueTask<Exception> ObserveInconsistentStateAsync(IAIAssetCatalogProvider provider)
        {
            var key = CreateKey();
            var urn = new AssetUrn($"urn:pulsestack:prompt:{Guid.NewGuid():N}");

            lock (catalogNamespace.SyncRoot)
            {
                // Real contradictory authority: exact publication exists without either lineage index.
                catalogNamespace.ExactRecords.Add(key, new CatalogRecord(key, urn));
            }

            try
            {
                await provider.FindExactAsync(key);
                return new InvalidOperationException("The contradictory namespace state was not observed.");
            }
            catch (Exception ex)
            {
                return ex;
            }
        }
    }

    [Fact]
    public void Provider_ShouldDeclareTransientCapability()
    {
        var provider = new InMemoryAIAssetCatalogProvider();

        provider.CapabilityProfile.Durability.Should().Be(AIAssetAuthorityDurability.Transient);
    }

    [Fact]
    public async Task PublishAsync_ShouldRejectConflictClassificationWhenObservedLineageAuthorityIsAlreadyContradictory()
    {
        var catalogNamespace = new InMemoryAIAssetCatalogNamespace();
        var provider = new InMemoryAIAssetCatalogProvider(catalogNamespace);
        var id = AssetId.New();
        var existingUrn = new AssetUrn($"urn:pulsestack:prompt:{Guid.NewGuid():N}");
        var candidateUrn = new AssetUrn($"urn:pulsestack:prompt:{Guid.NewGuid():N}");
        var candidate = new CatalogRecord(
            new AssetDefinitionKey(AssetType.Prompt, id, new AssetVersion("2.0")),
            candidateUrn);

        lock (catalogNamespace.SyncRoot)
        {
            // Reverse lineage mapping exists, but its authoritative lineage record is missing.
            catalogNamespace.UrnByLineage.Add((AssetType.Prompt, id), existingUrn);
        }

        var act = async () => await provider.PublishAsync(candidate);

        var exception = await act.Should().ThrowAsync<AIAssetCatalogException>();
        exception.Which.Category.Should().Be(AIAssetCatalogFailureCategory.InconsistentState);
    }

    [Fact]
    public async Task FindLineageAsync_ShouldRejectIncompleteLineageMembershipRelativeToExactAuthority()
    {
        var catalogNamespace = new InMemoryAIAssetCatalogNamespace();
        var provider = new InMemoryAIAssetCatalogProvider(catalogNamespace);
        var id = AssetId.New();
        var urn = new AssetUrn($"urn:pulsestack:prompt:{Guid.NewGuid():N}");
        var version1 = new AssetVersion("1.0");
        var version2 = new AssetVersion("2.0");
        var key1 = new AssetDefinitionKey(AssetType.Prompt, id, version1);
        var key2 = new AssetDefinitionKey(AssetType.Prompt, id, version2);

        lock (catalogNamespace.SyncRoot)
        {
            catalogNamespace.UrnByLineage.Add((AssetType.Prompt, id), urn);
            catalogNamespace.LineagesByUrn.Add(
                urn,
                new InMemoryCatalogLineageState(AssetType.Prompt, id, urn, version1));
            catalogNamespace.ExactRecords.Add(key1, new CatalogRecord(key1, urn));
            catalogNamespace.ExactRecords.Add(key2, new CatalogRecord(key2, urn));
        }

        var act = async () => await provider.FindLineageAsync(urn);

        var exception = await act.Should().ThrowAsync<AIAssetCatalogException>();
        exception.Which.Category.Should().Be(AIAssetCatalogFailureCategory.InconsistentState);
    }

    private static AssetDefinitionKey CreateKey() =>
        new(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);
}
