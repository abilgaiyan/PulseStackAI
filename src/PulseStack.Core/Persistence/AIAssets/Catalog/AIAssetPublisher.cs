using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;

namespace PulseStack.Core.Persistence.AIAssets.Catalog;

public sealed class AIAssetPublisher : IAIAssetPublisher
{
    private readonly IAIAssetCatalogProvider catalog;
    private readonly IAIAssetLoader loader;

    public AIAssetPublisher(IAIAssetCatalogProvider catalog, IAIAssetLoader loader)
    {
        this.catalog = catalog ?? throw CompositionFailure(nameof(catalog));
        this.loader = loader ?? throw CompositionFailure(nameof(loader));
    }

    public async ValueTask<AIAssetPublicationResult> PublishAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default)
    {
        AIAssetStorageContract.EnsureValidKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        var lookup = await FindExactAsync(key, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (lookup is ExactCatalogLookupResult.Found found)
        {
            PersistentAIAssetResolver.EnsureExactRecordMatches(key, found.Record, "Publish");
            var existing = await LoadAsync(key, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (existing is AIAssetLoadResult.NotFound)
            {
                throw new AIAssetCatalogBoundaryException(
                    AIAssetCatalogBoundaryFailureCategory.PublishedDefinitionUnavailable,
                    "The catalog identifies the exact definition as published, but MS-009.7 loading reported it absent.",
                    new AIAssetCatalogDiagnosticContext("Publish", key, found.Record.Urn));
            }

            var asset = ((AIAssetLoadResult.Loaded)existing).Asset;
            EnsureLoadedUrnMatches(asset, found.Record.Urn, key);
            return AIAssetPublicationResult.AlreadyPublished;
        }

        var load = await LoadAsync(key, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (load is AIAssetLoadResult.NotFound)
        {
            return AIAssetPublicationResult.DefinitionNotStored;
        }

        var loaded = ((AIAssetLoadResult.Loaded)load).Asset;
        var record = new CatalogRecord(key, loaded.Urn);

        cancellationToken.ThrowIfCancellationRequested();
        var publication = await PublishRecordAsync(record, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        return publication switch
        {
            CatalogPublicationResult.Created => AIAssetPublicationResult.Published,
            CatalogPublicationResult.AlreadyPresent => AIAssetPublicationResult.AlreadyPublished,
            CatalogPublicationResult.Conflict => AIAssetPublicationResult.IdentityConflict,
            _ => throw PersistentAIAssetResolver.Inconsistent(
                "The catalog provider returned an unsupported publication outcome.",
                "Publish",
                key,
                loaded.Urn)
        };
    }

    private async ValueTask<ExactCatalogLookupResult> FindExactAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken)
    {
        try
        {
            return await catalog.FindExactAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw PersistentAIAssetResolver.ProviderFailure("FindExact", key: key);
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw PersistentAIAssetResolver.ProviderFailure("FindExact", key: key, innerException: exception);
        }
    }

    private async ValueTask<CatalogPublicationResult> PublishRecordAsync(
        CatalogRecord record,
        CancellationToken cancellationToken)
    {
        try
        {
            return await catalog.PublishAsync(record, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw PersistentAIAssetResolver.ProviderFailure(
                "Publish",
                record.DefinitionKey,
                record.Urn);
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw PersistentAIAssetResolver.ProviderFailure(
                "Publish",
                record.DefinitionKey,
                record.Urn,
                exception);
        }
    }

    private ValueTask<AIAssetLoadResult> LoadAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken) =>
        loader.LoadAsync(key, cancellationToken);

    private static void EnsureLoadedUrnMatches(IAsset asset, AssetUrn catalogUrn, AssetDefinitionKey key)
    {
        if (!Equals(asset.Urn, catalogUrn))
        {
            throw new AIAssetCatalogBoundaryException(
                AIAssetCatalogBoundaryFailureCategory.CatalogAssetIdentityMismatch,
                "The loaded AI Asset URN disagrees with the published catalog identity.",
                new AIAssetCatalogDiagnosticContext("Publish", key, catalogUrn));
        }
    }

    private static AIAssetCatalogException CompositionFailure(string dependency) =>
        new(
            AIAssetCatalogFailureCategory.CompositionConfiguration,
            $"A required persistent AI Asset catalog dependency is missing: {dependency}.");
}
