using System.Globalization;
using System.Text;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;

namespace PulseStack.Core.Persistence.AIAssets.Storage;

/// <summary>
/// Stores exact serialized AI Asset representations in a filesystem namespace using immutable first-write semantics.
/// </summary>
public sealed class FileSerializedAIAssetStore : ISerializedAIAssetStore
{
    private const string AssetFileExtension = ".asset";
    private const string TemporaryFileExtension = ".tmp";

    private readonly string rootPath;
    private readonly IFileSerializedAIAssetStoreFaultInjector faultInjector;

    public FileSerializedAIAssetStore(string rootPath)
        : this(rootPath, NoOpFileSerializedAIAssetStoreFaultInjector.Instance)
    {
    }

    internal FileSerializedAIAssetStore(
        string rootPath,
        IFileSerializedAIAssetStoreFaultInjector faultInjector)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.CompositionConfiguration,
                "A non-empty file-store root path is required.");
        }

        this.faultInjector = faultInjector ?? throw new ArgumentNullException(nameof(faultInjector));

        try
        {
            this.rootPath = Path.GetFullPath(rootPath);
            Directory.CreateDirectory(this.rootPath);
        }
        catch (Exception exception)
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.CompositionConfiguration,
                "The file-store root path could not be initialized.",
                innerException: exception);
        }
    }

    public async ValueTask<SerializedAIAssetReadResult> ReadAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default)
    {
        AIAssetStorageContract.EnsureValidKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        var assetPath = ResolveAssetPath(key);

        try
        {
            var representation = await File.ReadAllBytesAsync(assetPath, cancellationToken).ConfigureAwait(false);
            return new SerializedAIAssetReadResult.Found(representation);
        }
        catch (FileNotFoundException)
        {
            return new SerializedAIAssetReadResult.NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return new SerializedAIAssetReadResult.NotFound();
        }
    }

    public async ValueTask<AIAssetWriteResult> WriteAsync(
        AssetDefinitionKey key,
        ReadOnlyMemory<byte> representation,
        CancellationToken cancellationToken = default)
    {
        AIAssetStorageContract.EnsureValidKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        var assetPath = ResolveAssetPath(key);
        var directoryPath = Path.GetDirectoryName(assetPath)!;
        Directory.CreateDirectory(directoryPath);

        var temporaryPath = Path.Combine(
            directoryPath,
            $".{Path.GetFileName(assetPath)}.{Guid.NewGuid():N}{TemporaryFileExtension}");

        try
        {
            faultInjector.OnCheckpoint(
                FileSerializedAIAssetStoreWriteCheckpoint.BeforeTemporaryCreate,
                temporaryPath,
                assetPath);

            await WriteTemporaryAsync(temporaryPath, representation, cancellationToken).ConfigureAwait(false);

            faultInjector.OnCheckpoint(
                FileSerializedAIAssetStoreWriteCheckpoint.AfterTemporaryFlush,
                temporaryPath,
                assetPath);

            cancellationToken.ThrowIfCancellationRequested();

            faultInjector.OnCheckpoint(
                FileSerializedAIAssetStoreWriteCheckpoint.BeforePublication,
                temporaryPath,
                assetPath);

            try
            {
                File.Move(temporaryPath, assetPath, overwrite: false);
                return AIAssetWriteResult.Created;
            }
            catch (IOException) when (File.Exists(assetPath))
            {
                var stored = await File.ReadAllBytesAsync(assetPath, CancellationToken.None).ConfigureAwait(false);
                return stored.AsSpan().SequenceEqual(representation.Span)
                    ? AIAssetWriteResult.AlreadyPresent
                    : AIAssetWriteResult.Conflict;
            }
        }
        finally
        {
            TryDeleteTemporary(temporaryPath);
        }
    }

    internal string ResolveAssetPath(AssetDefinitionKey key)
    {
        AIAssetStorageContract.EnsureValidKey(key);

        var typeSegment = ((int)key.Type).ToString(CultureInfo.InvariantCulture);
        var idSegment = key.Id.Value.ToString("N");
        var versionSegment = EncodeUtf16CodeUnits(key.Version.Value);

        return Path.Combine(
            rootPath,
            typeSegment,
            idSegment,
            versionSegment + AssetFileExtension);
    }

    private static async ValueTask WriteTemporaryAsync(
        string temporaryPath,
        ReadOnlyMemory<byte> representation,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);

        await stream.WriteAsync(representation, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string EncodeUtf16CodeUnits(string value)
    {
        var builder = new StringBuilder(value.Length * 4);

        foreach (var codeUnit in value)
        {
            builder.Append(((int)codeUnit).ToString("X4", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static void TryDeleteTemporary(string path)
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
            // Temporary cleanup is best-effort. Publication state is determined only by the final asset path.
        }
    }
}

internal enum FileSerializedAIAssetStoreWriteCheckpoint
{
    BeforeTemporaryCreate,
    AfterTemporaryFlush,
    BeforePublication
}

internal interface IFileSerializedAIAssetStoreFaultInjector
{
    void OnCheckpoint(
        FileSerializedAIAssetStoreWriteCheckpoint checkpoint,
        string temporaryPath,
        string assetPath);
}

internal sealed class NoOpFileSerializedAIAssetStoreFaultInjector : IFileSerializedAIAssetStoreFaultInjector
{
    public static NoOpFileSerializedAIAssetStoreFaultInjector Instance { get; } = new();

    private NoOpFileSerializedAIAssetStoreFaultInjector()
    {
    }

    public void OnCheckpoint(
        FileSerializedAIAssetStoreWriteCheckpoint checkpoint,
        string temporaryPath,
        string assetPath)
    {
    }
}
