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
                () => catalogNamespace.TestState.LastExactLookupToken,
                () => catalogNamespace.TestState.LastLineageLookupToken,
                () => catalogNamespace.TestState.LastPublicationToken));

        return ValueTask.FromResult(fixture);

        async ValueTask<Exception> ObserveProviderFailureAsync(IAIAssetCatalogProvider provider)
        {
            lock (catalogNamespace.SyncRoot)
            {
                catalogNamespace.TestState.NextProviderFailure = new IOException("Injected in-memory provider failure.");
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
            lock (catalogNamespace.SyncRoot)
            {
                catalogNamespace.TestState.CorruptNextExactLookup = true;
            }

            try
            {
                await provider.FindExactAsync(CreateKey());
                return new InvalidOperationException("The injected inconsistent state was not observed.");
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

    private static AssetDefinitionKey CreateKey() =>
        new(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);
}
