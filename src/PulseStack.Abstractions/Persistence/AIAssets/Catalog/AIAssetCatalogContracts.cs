using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.Catalog;

/// <summary>
/// Portable provider contract for one persistent AI Asset catalog authority.
/// </summary>
public interface IAIAssetCatalogProvider
{
    ValueTask<ExactCatalogLookupResult> FindExactAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default);

    ValueTask<CatalogLineageLookupResult> FindLineageAsync(
        AssetUrn urn,
        CancellationToken cancellationToken = default);

    ValueTask<CatalogPublicationResult> PublishAsync(
        CatalogRecord record,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Makes one already-stored exact AI Asset definition observable through the persistent catalog.
/// </summary>
public interface IAIAssetPublisher
{
    ValueTask<AIAssetPublicationResult> PublishAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Resolves published declarative AI Asset definitions through the persistent catalog and MS-009.7 loader.
/// This contract performs no runtime realization or recursive graph loading.
/// </summary>
public interface IPersistentAIAssetResolver
{
    ValueTask<AIAssetResolutionResult> ResolveAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default);

    ValueTask<AIAssetResolutionResult> ResolveAsync(
        AssetReference reference,
        CancellationToken cancellationToken = default);

    ValueTask<AIAssetResolutionResult> ResolveAsync(
        AssetUrn urn,
        AssetVersion version,
        CancellationToken cancellationToken = default);

    ValueTask<CatalogLineageLookupResult> DiscoverLineageAsync(
        AssetUrn urn,
        CancellationToken cancellationToken = default);
}
