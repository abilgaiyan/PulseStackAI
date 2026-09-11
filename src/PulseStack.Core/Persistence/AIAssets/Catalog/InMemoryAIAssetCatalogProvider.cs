using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;

namespace PulseStack.Core.Persistence.AIAssets.Catalog;

/// <summary>
/// Transient in-memory implementation of the persistent AI Asset catalog provider contract.
/// Multiple provider instances may share one <see cref="InMemoryAIAssetCatalogNamespace"/>.
/// </summary>
public sealed class InMemoryAIAssetCatalogProvider : IAIAssetCatalogProvider
{
    private readonly InMemoryAIAssetCatalogNamespace catalogNamespace;

    public InMemoryAIAssetCatalogProvider()
        : this(new InMemoryAIAssetCatalogNamespace())
    {
    }

    public InMemoryAIAssetCatalogProvider(InMemoryAIAssetCatalogNamespace catalogNamespace)
    {
        this.catalogNamespace = catalogNamespace ?? throw new ArgumentNullException(nameof(catalogNamespace));
    }

    public AIAssetCatalogCapabilityProfile CapabilityProfile { get; } =
        new(AIAssetAuthorityDurability.Transient);

    public ValueTask<ExactCatalogLookupResult> FindExactAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default)
    {
        AIAssetStorageContract.EnsureValidKey(key);
        ObserveExactToken(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            ThrowInjectedProviderFailureIfAny();

            lock (catalogNamespace.SyncRoot)
            {
                if (!catalogNamespace.ExactRecords.TryGetValue(key, out var record))
                {
                    return ValueTask.FromResult<ExactCatalogLookupResult>(new ExactCatalogLookupResult.NotFound());
                }

                EnsureExactStateCoherent(key, record, "FindExact");
                return ValueTask.FromResult<ExactCatalogLookupResult>(
                    new ExactCatalogLookupResult.Found(new CatalogRecord(record.DefinitionKey, record.Urn)));
            }
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ProviderFailure("FindExact", key, null, ex);
        }
    }

    public ValueTask<CatalogLineageLookupResult> FindLineageAsync(
        AssetUrn urn,
        CancellationToken cancellationToken = default)
    {
        EnsureValidUrn(urn);
        ObserveLineageToken(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            ThrowInjectedProviderFailureIfAny();

            lock (catalogNamespace.SyncRoot)
            {
                if (!catalogNamespace.LineagesByUrn.TryGetValue(urn, out var lineage))
                {
                    return ValueTask.FromResult<CatalogLineageLookupResult>(new CatalogLineageLookupResult.NotFound());
                }

                if (lineage.Urn != urn)
                {
                    throw Inconsistent(
                        "The in-memory catalog lineage record does not match its authority URN.",
                        new AIAssetCatalogDiagnosticContext("FindLineage", Urn: urn));
                }

                EnsureLineageStateCoherent(lineage, "FindLineage");
                return ValueTask.FromResult<CatalogLineageLookupResult>(
                    new CatalogLineageLookupResult.Found(
                        new CatalogLineage(lineage.Type, lineage.Id, lineage.Urn, lineage.Versions.ToArray())));
            }
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ProviderFailure("FindLineage", null, urn, ex);
        }
    }

    public ValueTask<CatalogPublicationResult> PublishAsync(
        CatalogRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        ObservePublicationToken(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            ThrowInjectedProviderFailureIfAny();

            lock (catalogNamespace.SyncRoot)
            {
                // Existing authority must be internally coherent before it is allowed to
                // classify the candidate as AlreadyPresent or Conflict.
                EnsureNamespaceStateCoherent("Publish");

                var key = record.DefinitionKey;
                var lineageIdentity = (key.Type, key.Id);

                if (catalogNamespace.ExactRecords.TryGetValue(key, out var existingExact))
                {
                    return ValueTask.FromResult(
                        existingExact.Urn == record.Urn
                            ? CatalogPublicationResult.AlreadyPresent
                            : CatalogPublicationResult.Conflict);
                }

                if (catalogNamespace.UrnByLineage.TryGetValue(lineageIdentity, out var existingUrn)
                    && existingUrn != record.Urn)
                {
                    return ValueTask.FromResult(CatalogPublicationResult.Conflict);
                }

                if (catalogNamespace.LineagesByUrn.TryGetValue(record.Urn, out var existingLineage)
                    && (existingLineage.Type != key.Type || existingLineage.Id != key.Id))
                {
                    return ValueTask.FromResult(CatalogPublicationResult.Conflict);
                }

                var ownedRecord = new CatalogRecord(key, record.Urn);
                catalogNamespace.ExactRecords.Add(key, ownedRecord);

                if (existingLineage is null)
                {
                    catalogNamespace.UrnByLineage.Add(lineageIdentity, record.Urn);
                    catalogNamespace.LineagesByUrn.Add(
                        record.Urn,
                        new InMemoryCatalogLineageState(key.Type, key.Id, record.Urn, key.Version));
                }
                else
                {
                    existingLineage.Versions.Add(key.Version);
                }

                return ValueTask.FromResult(CatalogPublicationResult.Created);
            }
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw ProviderFailure("Publish", record.DefinitionKey, record.Urn, ex);
        }
    }

    private void EnsureNamespaceStateCoherent(string operation)
    {
        foreach (var (identity, urn) in catalogNamespace.UrnByLineage)
        {
            if (!catalogNamespace.LineagesByUrn.TryGetValue(urn, out var lineage)
                || lineage.Type != identity.Type
                || lineage.Id != identity.Id
                || lineage.Urn != urn)
            {
                throw Inconsistent(
                    "The in-memory catalog lineage indexes disagree.",
                    new AIAssetCatalogDiagnosticContext(operation, Urn: urn));
            }
        }

        foreach (var (urn, lineage) in catalogNamespace.LineagesByUrn)
        {
            if (lineage.Urn != urn)
            {
                throw Inconsistent(
                    "The in-memory catalog lineage record does not match its authority URN.",
                    new AIAssetCatalogDiagnosticContext(operation, Urn: urn));
            }

            EnsureLineageStateCoherent(lineage, operation);
        }

        foreach (var (key, record) in catalogNamespace.ExactRecords)
        {
            EnsureExactStateCoherent(key, record, operation);
        }
    }

    private void EnsureExactStateCoherent(
        AssetDefinitionKey requestedKey,
        CatalogRecord record,
        string operation)
    {
        if (record.DefinitionKey != requestedKey)
        {
            throw Inconsistent(
                "The in-memory catalog exact record does not match its authority key.",
                new AIAssetCatalogDiagnosticContext(operation, requestedKey));
        }

        var lineageIdentity = (record.DefinitionKey.Type, record.DefinitionKey.Id);
        if (!catalogNamespace.UrnByLineage.TryGetValue(lineageIdentity, out var urn)
            || urn != record.Urn
            || !catalogNamespace.LineagesByUrn.TryGetValue(record.Urn, out var lineage)
            || lineage.Type != record.DefinitionKey.Type
            || lineage.Id != record.DefinitionKey.Id
            || lineage.Urn != record.Urn
            || !lineage.Versions.Contains(record.DefinitionKey.Version))
        {
            throw Inconsistent(
                "The in-memory catalog exact and lineage authorities disagree.",
                new AIAssetCatalogDiagnosticContext(operation, requestedKey, record.Urn));
        }
    }

    private void EnsureLineageStateCoherent(
        InMemoryCatalogLineageState lineage,
        string operation)
    {
        if (!catalogNamespace.UrnByLineage.TryGetValue((lineage.Type, lineage.Id), out var urn)
            || urn != lineage.Urn
            || lineage.Versions.Count == 0)
        {
            throw Inconsistent(
                "The in-memory catalog lineage authority is inconsistent.",
                new AIAssetCatalogDiagnosticContext(operation, Urn: lineage.Urn));
        }

        var relatedExactRecords = catalogNamespace.ExactRecords
            .Where(pair =>
                (pair.Key.Type == lineage.Type && pair.Key.Id == lineage.Id)
                || pair.Value.Urn == lineage.Urn)
            .ToArray();

        foreach (var (key, record) in relatedExactRecords)
        {
            if (record.DefinitionKey != key
                || key.Type != lineage.Type
                || key.Id != lineage.Id
                || record.Urn != lineage.Urn)
            {
                throw Inconsistent(
                    "The in-memory catalog exact authority conflicts with lineage identity.",
                    new AIAssetCatalogDiagnosticContext(operation, key, lineage.Urn));
            }
        }

        foreach (var version in lineage.Versions)
        {
            var key = new AssetDefinitionKey(lineage.Type, lineage.Id, version);
            if (!catalogNamespace.ExactRecords.TryGetValue(key, out var record)
                || record.DefinitionKey != key
                || record.Urn != lineage.Urn)
            {
                throw Inconsistent(
                    "The in-memory catalog lineage membership has no coherent exact authority.",
                    new AIAssetCatalogDiagnosticContext(operation, key, lineage.Urn));
            }
        }

        var exactVersions = relatedExactRecords
            .Select(pair => pair.Key.Version)
            .ToHashSet();

        if (!exactVersions.SetEquals(lineage.Versions))
        {
            throw Inconsistent(
                "The in-memory catalog lineage membership is incomplete relative to exact authority.",
                new AIAssetCatalogDiagnosticContext(operation, Urn: lineage.Urn));
        }
    }

    private void ThrowInjectedProviderFailureIfAny()
    {
        lock (catalogNamespace.SyncRoot)
        {
            if (catalogNamespace.TestHooks.NextProviderFailure is not { } failure)
            {
                return;
            }

            catalogNamespace.TestHooks.NextProviderFailure = null;
            throw failure;
        }
    }

    private void ObserveExactToken(CancellationToken token)
    {
        lock (catalogNamespace.SyncRoot)
        {
            catalogNamespace.TestHooks.LastExactLookupToken = token;
        }
    }

    private void ObserveLineageToken(CancellationToken token)
    {
        lock (catalogNamespace.SyncRoot)
        {
            catalogNamespace.TestHooks.LastLineageLookupToken = token;
        }
    }

    private void ObservePublicationToken(CancellationToken token)
    {
        lock (catalogNamespace.SyncRoot)
        {
            catalogNamespace.TestHooks.LastPublicationToken = token;
        }
    }

    private static void EnsureValidUrn(AssetUrn urn)
    {
        ArgumentNullException.ThrowIfNull(urn);
        if (string.IsNullOrWhiteSpace(urn.Value))
        {
            throw new ArgumentException("Catalog URN must not be empty or whitespace.", nameof(urn));
        }
    }

    private static AIAssetCatalogException ProviderFailure(
        string operation,
        AssetDefinitionKey? key,
        AssetUrn? urn,
        Exception innerException) =>
        new(
            AIAssetCatalogFailureCategory.ProviderFailure,
            "The in-memory AI Asset catalog provider could not complete the operation.",
            new AIAssetCatalogDiagnosticContext(operation, key, urn),
            innerException);

    private static AIAssetCatalogException Inconsistent(
        string message,
        AIAssetCatalogDiagnosticContext context) =>
        new(AIAssetCatalogFailureCategory.InconsistentState, message, context);
}
