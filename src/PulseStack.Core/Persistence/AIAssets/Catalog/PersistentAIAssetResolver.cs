using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;

namespace PulseStack.Core.Persistence.AIAssets.Catalog;

public sealed class PersistentAIAssetResolver : IPersistentAIAssetResolver
{
    private readonly IAIAssetCatalogProvider catalog;
    private readonly IAIAssetLoader loader;

    public PersistentAIAssetResolver(IAIAssetCatalogProvider catalog, IAIAssetLoader loader)
    {
        this.catalog = catalog ?? throw CompositionFailure(nameof(catalog));
        this.loader = loader ?? throw CompositionFailure(nameof(loader));
    }

    public async ValueTask<AIAssetResolutionResult> ResolveAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default)
    {
        AIAssetStorageContract.EnsureValidKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        var lookup = await FindExactAsync(key, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (lookup is ExactCatalogLookupResult.NotFound)
        {
            return new AIAssetResolutionResult.DefinitionNotPublished();
        }

        var record = ((ExactCatalogLookupResult.Found)lookup).Record;
        EnsureExactRecordMatches(key, record, "ResolveExact");

        var asset = await LoadPublishedAsync(key, record.Urn, "ResolveExact", cancellationToken).ConfigureAwait(false);
        return new AIAssetResolutionResult.Resolved(asset);
    }

    public async ValueTask<AIAssetResolutionResult> ResolveAsync(
        AssetReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var key = AssetDefinitionKey.From(reference);
        AIAssetStorageContract.EnsureValidKey(key);
        EnsureValidUrn(reference.Urn, nameof(reference));
        cancellationToken.ThrowIfCancellationRequested();

        var lookup = await FindExactAsync(key, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (lookup is ExactCatalogLookupResult.NotFound)
        {
            return new AIAssetResolutionResult.DefinitionNotPublished();
        }

        var record = ((ExactCatalogLookupResult.Found)lookup).Record;
        EnsureExactRecordMatches(key, record, "ResolveReference");
        if (!Equals(record.Urn, reference.Urn))
        {
            return new AIAssetResolutionResult.ReferenceMismatch();
        }

        var asset = await LoadPublishedAsync(key, record.Urn, "ResolveReference", cancellationToken).ConfigureAwait(false);
        return new AIAssetResolutionResult.Resolved(asset);
    }

    public async ValueTask<AIAssetResolutionResult> ResolveAsync(
        AssetUrn urn,
        AssetVersion version,
        CancellationToken cancellationToken = default)
    {
        EnsureValidUrn(urn, nameof(urn));
        EnsureValidVersion(version, nameof(version));
        cancellationToken.ThrowIfCancellationRequested();

        var lineageLookup = await FindLineageAsync(urn, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (lineageLookup is CatalogLineageLookupResult.NotFound)
        {
            return new AIAssetResolutionResult.LineageNotPublished();
        }

        var lineage = ((CatalogLineageLookupResult.Found)lineageLookup).Lineage;
        EnsureLineageMatches(urn, lineage, "ResolveUrnVersion");
        if (!lineage.PublishedVersions.Contains(version))
        {
            return new AIAssetResolutionResult.DefinitionNotPublished();
        }

        var key = new AssetDefinitionKey(lineage.Type, lineage.Id, version);
        var exactLookup = await FindExactAsync(key, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (exactLookup is ExactCatalogLookupResult.NotFound)
        {
            throw Inconsistent("The catalog lineage contains a version whose exact record is absent.", "ResolveUrnVersion", key, urn);
        }

        var record = ((ExactCatalogLookupResult.Found)exactLookup).Record;
        EnsureExactRecordMatches(key, record, "ResolveUrnVersion");
        if (!Equals(record.Urn, urn))
        {
            throw Inconsistent("The exact catalog record disagrees with the requested lineage URN.", "ResolveUrnVersion", key, urn);
        }

        var asset = await LoadPublishedAsync(key, record.Urn, "ResolveUrnVersion", cancellationToken).ConfigureAwait(false);
        return new AIAssetResolutionResult.Resolved(asset);
    }

    public async ValueTask<CatalogLineageLookupResult> DiscoverLineageAsync(
        AssetUrn urn,
        CancellationToken cancellationToken = default)
    {
        EnsureValidUrn(urn, nameof(urn));
        cancellationToken.ThrowIfCancellationRequested();

        var result = await FindLineageAsync(urn, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (result is CatalogLineageLookupResult.Found found)
        {
            EnsureLineageMatches(urn, found.Lineage, "DiscoverLineage");
        }

        return result;
    }

    private async ValueTask<IAsset> LoadPublishedAsync(
        AssetDefinitionKey key,
        AssetUrn catalogUrn,
        string operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var load = await loader.LoadAsync(key, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (load is AIAssetLoadResult.NotFound)
        {
            throw new AIAssetCatalogBoundaryException(
                AIAssetCatalogBoundaryFailureCategory.PublishedDefinitionUnavailable,
                "The catalog identifies the exact definition as published, but MS-009.7 loading reported it absent.",
                new AIAssetCatalogDiagnosticContext(operation, key, catalogUrn));
        }

        var asset = ((AIAssetLoadResult.Loaded)load).Asset;
        if (!Equals(asset.Urn, catalogUrn))
        {
            throw new AIAssetCatalogBoundaryException(
                AIAssetCatalogBoundaryFailureCategory.CatalogAssetIdentityMismatch,
                "The loaded AI Asset URN disagrees with the published catalog identity.",
                new AIAssetCatalogDiagnosticContext(operation, key, catalogUrn));
        }

        return asset;
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
            throw ProviderFailure("FindExact", key: key);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw ProviderFailure("FindExact", key: key, innerException: exception);
        }
    }

    private async ValueTask<CatalogLineageLookupResult> FindLineageAsync(
        AssetUrn urn,
        CancellationToken cancellationToken)
    {
        try
        {
            return await catalog.FindLineageAsync(urn, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw ProviderFailure("FindLineage", urn: urn);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw ProviderFailure("FindLineage", urn: urn, innerException: exception);
        }
    }

    internal static void EnsureExactRecordMatches(AssetDefinitionKey key, CatalogRecord record, string operation)
    {
        if (record.DefinitionKey != key)
        {
            throw Inconsistent("The catalog provider returned an exact record for a different definition key.", operation, key, record.Urn);
        }
    }

    internal static void EnsureLineageMatches(AssetUrn requestedUrn, CatalogLineage lineage, string operation)
    {
        if (!Equals(lineage.Urn, requestedUrn))
        {
            throw Inconsistent("The catalog provider returned a lineage for a different URN.", operation, urn: requestedUrn);
        }

        var representativeVersion = lineage.PublishedVersions.FirstOrDefault();
        if (representativeVersion is null)
        {
            throw Inconsistent("The catalog provider returned an empty lineage.", operation, urn: requestedUrn);
        }

        try
        {
            AIAssetStorageContract.EnsureValidKey(new AssetDefinitionKey(lineage.Type, lineage.Id, representativeVersion));
        }
        catch (ArgumentException exception)
        {
            throw Inconsistent("The catalog provider returned an invalid lineage identity.", operation, urn: requestedUrn, innerException: exception);
        }

        if (lineage.PublishedVersions.Any(version => version is null || string.IsNullOrWhiteSpace(version.Value)))
        {
            throw Inconsistent("The catalog provider returned an invalid published-version identity.", operation, urn: requestedUrn);
        }
    }

    internal static void EnsureValidUrn(AssetUrn? urn, string parameterName)
    {
        if (urn is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (string.IsNullOrWhiteSpace(urn.Value))
        {
            throw new ArgumentException("The AssetUrn must not be empty or whitespace.", parameterName);
        }
    }

    internal static void EnsureValidVersion(AssetVersion? version, string parameterName)
    {
        if (version is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (string.IsNullOrWhiteSpace(version.Value))
        {
            throw new ArgumentException("The AssetVersion must not be empty or whitespace.", parameterName);
        }
    }

    internal static AIAssetCatalogException Inconsistent(
        string message,
        string operation,
        AssetDefinitionKey? key = null,
        AssetUrn? urn = null,
        Exception? innerException = null) =>
        new(
            AIAssetCatalogFailureCategory.InconsistentState,
            message,
            new AIAssetCatalogDiagnosticContext(operation, key, urn),
            innerException);

    internal static AIAssetCatalogException ProviderFailure(
        string operation,
        AssetDefinitionKey? key = null,
        AssetUrn? urn = null,
        Exception? innerException = null) =>
        new(
            AIAssetCatalogFailureCategory.ProviderFailure,
            "The persistent AI Asset catalog provider operation failed.",
            new AIAssetCatalogDiagnosticContext(operation, key, urn),
            innerException);

    private static AIAssetCatalogException CompositionFailure(string dependency) =>
        new(
            AIAssetCatalogFailureCategory.CompositionConfiguration,
            $"A required persistent AI Asset catalog dependency is missing: {dependency}.");
}
