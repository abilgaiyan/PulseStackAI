using System.Collections.Concurrent;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;

namespace PulseStack.Core.Persistence.AIAssets.Catalog;

/// <summary>
/// Durable file-backed catalog provider. The committed .catalog record set is authority;
/// process-local coordination only serializes participants that target the same normalized root.
/// </summary>
public sealed class FileAIAssetCatalogProvider : IAIAssetCatalogProvider
{
    private const string TemporaryExtension = ".tmp";
    private static readonly StringComparer RootComparer =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Coordinators =
        new(RootComparer);

    private readonly string rootPath;
    private readonly SemaphoreSlim coordinator;
    private readonly IFileAIAssetCatalogFaultInjector faultInjector;

    public FileAIAssetCatalogProvider(string rootPath)
        : this(rootPath, NoOpFileAIAssetCatalogFaultInjector.Instance)
    {
    }

    internal FileAIAssetCatalogProvider(
        string rootPath,
        IFileAIAssetCatalogFaultInjector faultInjector)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new AIAssetCatalogException(
                AIAssetCatalogFailureCategory.CompositionConfiguration,
                "A non-empty catalog root path is required.");
        }

        this.faultInjector = faultInjector ?? throw new ArgumentNullException(nameof(faultInjector));

        try
        {
            this.rootPath = NormalizeRoot(rootPath);
            Directory.CreateDirectory(this.rootPath);
            coordinator = Coordinators.GetOrAdd(this.rootPath, static _ => new SemaphoreSlim(1, 1));
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Failure(
                AIAssetCatalogFailureCategory.CompositionConfiguration,
                "The catalog root path could not be initialized.",
                "Initialize",
                inner: ex);
        }
    }

    public AIAssetCatalogCapabilityProfile CapabilityProfile { get; } =
        new(AIAssetAuthorityDurability.Durable);

    internal string RootPath => rootPath;

    public async ValueTask<ExactCatalogLookupResult> FindExactAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default)
    {
        AIAssetStorageContract.EnsureValidKey(key);
        ObserveToken(FileAIAssetCatalogOperation.ExactLookup, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        await coordinator.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            faultInjector.OnCheckpoint(FileAIAssetCatalogCheckpoint.BeforeAuthorityRead, rootPath, null);
            var authority = await LoadAuthorityAsync("FindExact", cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            return authority.Exact.TryGetValue(key, out var record)
                ? new ExactCatalogLookupResult.Found(record)
                : new ExactCatalogLookupResult.NotFound();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Failure(AIAssetCatalogFailureCategory.ProviderFailure, "File catalog exact lookup failed.", "FindExact", key, inner: ex);
        }
        finally
        {
            coordinator.Release();
        }
    }

    public async ValueTask<CatalogLineageLookupResult> FindLineageAsync(
        AssetUrn urn,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(urn);
        if (string.IsNullOrWhiteSpace(urn.Value))
        {
            throw new ArgumentException("Asset URN must not be empty or whitespace.", nameof(urn));
        }

        ObserveToken(FileAIAssetCatalogOperation.LineageLookup, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        await coordinator.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            faultInjector.OnCheckpoint(FileAIAssetCatalogCheckpoint.BeforeAuthorityRead, rootPath, null);
            var authority = await LoadAuthorityAsync("FindLineage", cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            return authority.Lineages.TryGetValue(urn, out var lineage)
                ? new CatalogLineageLookupResult.Found(lineage)
                : new CatalogLineageLookupResult.NotFound();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Failure(AIAssetCatalogFailureCategory.ProviderFailure, "File catalog lineage lookup failed.", "FindLineage", urn: urn, inner: ex);
        }
        finally
        {
            coordinator.Release();
        }
    }

    public async ValueTask<CatalogPublicationResult> PublishAsync(
        CatalogRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        AIAssetStorageContract.EnsureValidKey(record.DefinitionKey);
        ObserveToken(FileAIAssetCatalogOperation.Publication, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        byte[] representation;
        string committedPath;
        try
        {
            representation = AIAssetCatalogRecordCodec.Serialize(record);
            committedPath = AIAssetCatalogPathModel.GetRecordPath(rootPath, record.DefinitionKey);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw Failure(
                AIAssetCatalogFailureCategory.ProviderFailure,
                "The catalog publication candidate cannot be represented durably.",
                "Publish",
                record.DefinitionKey,
                record.Urn,
                ex);
        }

        await coordinator.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var authority = await LoadAuthorityAsync("Publish", cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            if (authority.Exact.TryGetValue(record.DefinitionKey, out var existing))
            {
                return existing.Urn == record.Urn
                    ? CatalogPublicationResult.AlreadyPresent
                    : CatalogPublicationResult.Conflict;
            }

            var lineageIdentity = (record.DefinitionKey.Type, record.DefinitionKey.Id);
            if (authority.UrnByLineage.TryGetValue(lineageIdentity, out var existingUrn)
                && existingUrn != record.Urn)
            {
                return CatalogPublicationResult.Conflict;
            }

            if (authority.IdentityByUrn.TryGetValue(record.Urn, out var existingIdentity)
                && existingIdentity != lineageIdentity)
            {
                return CatalogPublicationResult.Conflict;
            }

            var directory = Path.GetDirectoryName(committedPath)!;
            Directory.CreateDirectory(directory);
            var stagingPath = Path.Combine(
                directory,
                $".{Path.GetFileName(committedPath)}.{Guid.NewGuid():N}{TemporaryExtension}");

            try
            {
                faultInjector.OnCheckpoint(FileAIAssetCatalogCheckpoint.BeforeStagingCreate, stagingPath, committedPath);
                await WriteStagingAsync(stagingPath, representation, cancellationToken).ConfigureAwait(false);
                faultInjector.OnCheckpoint(FileAIAssetCatalogCheckpoint.AfterStagingFlush, stagingPath, committedPath);
                cancellationToken.ThrowIfCancellationRequested();
                faultInjector.OnCheckpoint(FileAIAssetCatalogCheckpoint.BeforeCommit, stagingPath, committedPath);

                try
                {
                    File.Move(stagingPath, committedPath, overwrite: false);
                    return CatalogPublicationResult.Created;
                }
                catch (IOException) when (File.Exists(committedPath))
                {
                    var committed = await ReadCommittedRecordAsync(committedPath, "Publish", cancellationToken).ConfigureAwait(false);
                    return committed.DefinitionKey == record.DefinitionKey && committed.Urn == record.Urn
                        ? CatalogPublicationResult.AlreadyPresent
                        : CatalogPublicationResult.Conflict;
                }
            }
            finally
            {
                TryDelete(stagingPath);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Failure(
                AIAssetCatalogFailureCategory.ProviderFailure,
                "File catalog publication failed before a committed authority cut was established.",
                "Publish",
                record.DefinitionKey,
                record.Urn,
                ex);
        }
        finally
        {
            coordinator.Release();
        }
    }

    internal static void ResetProcessCoordinationForTests(string rootPath)
    {
        var normalized = NormalizeRoot(rootPath);
        Coordinators.TryRemove(normalized, out _);
    }

    private async ValueTask<AuthoritySnapshot> LoadAuthorityAsync(
        string operation,
        CancellationToken cancellationToken)
    {
        var exact = new Dictionary<AssetDefinitionKey, CatalogRecord>();
        var urnByLineage = new Dictionary<(AssetType Type, AssetId Id), AssetUrn>();
        var identityByUrn = new Dictionary<AssetUrn, (AssetType Type, AssetId Id)>();
        var versionsByUrn = new Dictionary<AssetUrn, HashSet<AssetVersion>>();
        var recordsRoot = Path.Combine(rootPath, "records");

        if (!Directory.Exists(recordsRoot))
        {
            return new AuthoritySnapshot(exact, new Dictionary<AssetUrn, CatalogLineage>(), urnByLineage, identityByUrn);
        }

        foreach (var path in Directory.EnumerateFiles(recordsRoot, "*.catalog", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = await ReadCommittedRecordAsync(path, operation, cancellationToken).ConfigureAwait(false);
            var key = record.DefinitionKey;
            var identity = (key.Type, key.Id);

            if (!exact.TryAdd(key, record))
            {
                throw Inconsistent(operation, "Duplicate committed exact catalog identity was observed.", key, record.Urn);
            }

            if (urnByLineage.TryGetValue(identity, out var lineageUrn) && lineageUrn != record.Urn)
            {
                throw Inconsistent(operation, "Committed catalog authority maps one lineage identity to multiple URNs.", key, record.Urn);
            }

            urnByLineage[identity] = record.Urn;

            if (identityByUrn.TryGetValue(record.Urn, out var urnIdentity) && urnIdentity != identity)
            {
                throw Inconsistent(operation, "Committed catalog authority maps one URN to multiple lineage identities.", key, record.Urn);
            }

            identityByUrn[record.Urn] = identity;
            if (!versionsByUrn.TryGetValue(record.Urn, out var versions))
            {
                versions = new HashSet<AssetVersion>();
                versionsByUrn.Add(record.Urn, versions);
            }

            versions.Add(key.Version);
        }

        var lineages = new Dictionary<AssetUrn, CatalogLineage>();
        foreach (var (urn, identity) in identityByUrn)
        {
            lineages.Add(urn, new CatalogLineage(identity.Type, identity.Id, urn, versionsByUrn[urn]));
        }

        return new AuthoritySnapshot(exact, lineages, urnByLineage, identityByUrn);
    }

    private async ValueTask<CatalogRecord> ReadCommittedRecordAsync(
        string path,
        string operation,
        CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            if (info.Length > AIAssetCatalogRecordCodec.MaximumRecordBytes)
            {
                throw new InvalidDataException("Committed catalog record exceeds the portable record limit.");
            }

            var bytes = new byte[checked((int)info.Length)];
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);

            var record = AIAssetCatalogRecordCodec.Deserialize(bytes);
            AIAssetCatalogPathModel.ValidateRecordPath(rootPath, path, record);
            return record;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (AIAssetCatalogException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw Failure(
                AIAssetCatalogFailureCategory.InconsistentState,
                "Committed catalog authority is malformed or cannot be interpreted coherently.",
                operation,
                inner: ex);
        }
    }

    private static async ValueTask WriteStagingAsync(
        string path,
        ReadOnlyMemory<byte> representation,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);

        await stream.WriteAsync(representation, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        stream.Flush(flushToDisk: true);
    }

    private void ObserveToken(FileAIAssetCatalogOperation operation, CancellationToken token) =>
        faultInjector.ObserveToken(operation, token);

    private static string NormalizeRoot(string rootPath) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Staging cleanup is best-effort and never changes committed authority.
        }
    }

    private static AIAssetCatalogException Inconsistent(
        string operation,
        string message,
        AssetDefinitionKey? key = null,
        AssetUrn? urn = null,
        Exception? inner = null) =>
        Failure(AIAssetCatalogFailureCategory.InconsistentState, message, operation, key, urn, inner);

    private static AIAssetCatalogException Failure(
        AIAssetCatalogFailureCategory category,
        string message,
        string operation,
        AssetDefinitionKey? key = null,
        AssetUrn? urn = null,
        Exception? inner = null) =>
        new(category, message, new AIAssetCatalogDiagnosticContext(operation, key, urn), inner);

    private sealed record AuthoritySnapshot(
        Dictionary<AssetDefinitionKey, CatalogRecord> Exact,
        Dictionary<AssetUrn, CatalogLineage> Lineages,
        Dictionary<(AssetType Type, AssetId Id), AssetUrn> UrnByLineage,
        Dictionary<AssetUrn, (AssetType Type, AssetId Id)> IdentityByUrn);
}

internal enum FileAIAssetCatalogOperation
{
    ExactLookup,
    LineageLookup,
    Publication
}

internal enum FileAIAssetCatalogCheckpoint
{
    BeforeAuthorityRead,
    BeforeStagingCreate,
    AfterStagingFlush,
    BeforeCommit
}

internal interface IFileAIAssetCatalogFaultInjector
{
    void ObserveToken(FileAIAssetCatalogOperation operation, CancellationToken token);

    void OnCheckpoint(FileAIAssetCatalogCheckpoint checkpoint, string firstPath, string? secondPath);
}

internal sealed class NoOpFileAIAssetCatalogFaultInjector : IFileAIAssetCatalogFaultInjector
{
    public static NoOpFileAIAssetCatalogFaultInjector Instance { get; } = new();

    private NoOpFileAIAssetCatalogFaultInjector()
    {
    }

    public void ObserveToken(FileAIAssetCatalogOperation operation, CancellationToken token)
    {
    }

    public void OnCheckpoint(FileAIAssetCatalogCheckpoint checkpoint, string firstPath, string? secondPath)
    {
    }
}
