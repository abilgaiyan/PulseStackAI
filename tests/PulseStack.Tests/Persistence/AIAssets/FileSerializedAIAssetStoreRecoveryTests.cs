using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.Persistence.AIAssets.Storage;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class FileSerializedAIAssetStoreRecoveryTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "PulseStack.Tests",
        nameof(FileSerializedAIAssetStoreRecoveryTests),
        Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(FileSerializedAIAssetStoreWriteCheckpoint.BeforeTemporaryCreate)]
    [InlineData(FileSerializedAIAssetStoreWriteCheckpoint.AfterTemporaryFlush)]
    [InlineData(FileSerializedAIAssetStoreWriteCheckpoint.BeforePublication)]
    public async Task FailureBeforePublication_ShouldPublishNothingAndCleanTemporaryArtifacts(
        FileSerializedAIAssetStoreWriteCheckpoint checkpoint)
    {
        var key = CreateKey();
        var injector = new ThrowingFaultInjector(checkpoint);
        var store = new FileSerializedAIAssetStore(rootPath, injector);
        var assetPath = store.ResolveAssetPath(key);

        var act = async () => await store.WriteAsync(key, new byte[] { 1, 2, 3, 4 });

        await act.Should().ThrowAsync<IOException>();
        File.Exists(assetPath).Should().BeFalse();
        EnumerateTemporaryFiles().Should().BeEmpty();

        var restarted = new FileSerializedAIAssetStore(rootPath);
        (await restarted.ReadAsync(key)).Should().BeOfType<SerializedAIAssetReadResult.NotFound>();
    }

    [Fact]
    public async Task CancellationAfterTemporaryFlush_ShouldPublishNothingAndCleanTemporaryArtifact()
    {
        var key = CreateKey();
        using var source = new CancellationTokenSource();
        var injector = new CancellingFaultInjector(
            FileSerializedAIAssetStoreWriteCheckpoint.AfterTemporaryFlush,
            source);
        var store = new FileSerializedAIAssetStore(rootPath, injector);
        var assetPath = store.ResolveAssetPath(key);

        var act = async () => await store.WriteAsync(key, new byte[] { 5, 6, 7, 8 }, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        File.Exists(assetPath).Should().BeFalse();
        EnumerateTemporaryFiles().Should().BeEmpty();

        var restarted = new FileSerializedAIAssetStore(rootPath);
        (await restarted.ReadAsync(key)).Should().BeOfType<SerializedAIAssetReadResult.NotFound>();
    }

    [Fact]
    public async Task Publication_ShouldKeepFinalAbsentUntilOneCompleteTemporaryFileIsPromoted()
    {
        var key = CreateKey();
        var bytes = Enumerable.Repeat((byte)0x6D, 512 * 1024).ToArray();
        using var injector = new PausingFaultInjector(FileSerializedAIAssetStoreWriteCheckpoint.BeforePublication);
        var store = new FileSerializedAIAssetStore(rootPath, injector);
        var assetPath = store.ResolveAssetPath(key);

        var writeTask = Task.Run(async () => await store.WriteAsync(key, bytes));

        injector.WaitUntilEntered();

        File.Exists(assetPath).Should().BeFalse();
        injector.TemporaryPath.Should().NotBeNull();
        File.Exists(injector.TemporaryPath!).Should().BeTrue();
        File.ReadAllBytes(injector.TemporaryPath!).Should().Equal(bytes);

        var concurrentReader = new FileSerializedAIAssetStore(rootPath);
        (await concurrentReader.ReadAsync(key)).Should().BeOfType<SerializedAIAssetReadResult.NotFound>();

        injector.Release();
        (await writeTask).Should().Be(AIAssetWriteResult.Created);

        File.Exists(assetPath).Should().BeTrue();
        File.ReadAllBytes(assetPath).Should().Equal(bytes);
        EnumerateTemporaryFiles().Should().BeEmpty();

        var restarted = new FileSerializedAIAssetStore(rootPath);
        var found = (SerializedAIAssetReadResult.Found)await restarted.ReadAsync(key);
        found.Representation.ToArray().Should().Equal(bytes);
    }

    [Fact]
    public async Task RestartAfterCreated_ShouldPreserveImmutableWriteAlgebra()
    {
        var key = CreateKey();
        byte[] original = [11, 12, 13, 14];
        byte[] conflicting = [21, 22, 23, 24];

        var firstProcess = new FileSerializedAIAssetStore(rootPath);
        (await firstProcess.WriteAsync(key, original)).Should().Be(AIAssetWriteResult.Created);

        var restartedReader = new FileSerializedAIAssetStore(rootPath);
        var found = (SerializedAIAssetReadResult.Found)await restartedReader.ReadAsync(key);
        found.Representation.ToArray().Should().Equal(original);

        var restartedIdenticalWriter = new FileSerializedAIAssetStore(rootPath);
        (await restartedIdenticalWriter.WriteAsync(key, original)).Should().Be(AIAssetWriteResult.AlreadyPresent);

        var restartedConflictingWriter = new FileSerializedAIAssetStore(rootPath);
        (await restartedConflictingWriter.WriteAsync(key, conflicting)).Should().Be(AIAssetWriteResult.Conflict);

        var finalReader = new FileSerializedAIAssetStore(rootPath);
        var final = (SerializedAIAssetReadResult.Found)await finalReader.ReadAsync(key);
        final.Representation.ToArray().Should().Equal(original);
    }

    [Fact]
    public async Task OrphanTemporaryFile_ShouldBeIgnoredAcrossRestartAndNotBlockPublication()
    {
        var key = CreateKey();
        byte[] orphanBytes = [91, 92, 93];
        byte[] authoritativeBytes = [31, 32, 33];
        var firstProcess = new FileSerializedAIAssetStore(rootPath);
        var assetPath = firstProcess.ResolveAssetPath(key);
        var directoryPath = Path.GetDirectoryName(assetPath)!;
        Directory.CreateDirectory(directoryPath);
        var orphanPath = Path.Combine(
            directoryPath,
            $".{Path.GetFileName(assetPath)}.{Guid.NewGuid():N}.tmp");
        await File.WriteAllBytesAsync(orphanPath, orphanBytes);

        var restarted = new FileSerializedAIAssetStore(rootPath);
        (await restarted.ReadAsync(key)).Should().BeOfType<SerializedAIAssetReadResult.NotFound>();

        (await restarted.WriteAsync(key, authoritativeBytes)).Should().Be(AIAssetWriteResult.Created);

        var found = (SerializedAIAssetReadResult.Found)await new FileSerializedAIAssetStore(rootPath).ReadAsync(key);
        found.Representation.ToArray().Should().Equal(authoritativeBytes);
        File.ReadAllBytes(orphanPath).Should().Equal(orphanBytes);
    }

    [Fact]
    public async Task ConcurrentPublicationAcrossRestartEquivalentInstances_ShouldExposeOnlyWinningFinalFile()
    {
        var key = CreateKey();
        var candidates = Enumerable.Range(1, 24)
            .Select(index => Enumerable.Repeat((byte)index, 128 * 1024).ToArray())
            .ToArray();
        var stores = Enumerable.Range(0, candidates.Length)
            .Select(_ => new FileSerializedAIAssetStore(rootPath))
            .ToArray();

        using var ready = new SemaphoreSlim(0, stores.Length);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = stores.Select((store, index) => Task.Run(async () =>
        {
            ready.Release();
            await start.Task;
            return await store.WriteAsync(key, candidates[index]);
        })).ToArray();

        for (var index = 0; index < stores.Length; index++)
        {
            await ready.WaitAsync();
        }

        start.SetResult();
        var results = await Task.WhenAll(tasks);

        var winnerIndex = Array.FindIndex(results, result => result == AIAssetWriteResult.Created);
        winnerIndex.Should().BeGreaterThanOrEqualTo(0);
        results.Count(result => result == AIAssetWriteResult.Created).Should().Be(1);
        results.Count(result => result == AIAssetWriteResult.Conflict).Should().Be(candidates.Length - 1);

        var restarted = new FileSerializedAIAssetStore(rootPath);
        var found = (SerializedAIAssetReadResult.Found)await restarted.ReadAsync(key);
        found.Representation.ToArray().Should().Equal(candidates[winnerIndex]);
        EnumerateTemporaryFiles().Should().BeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private string[] EnumerateTemporaryFiles() =>
        Directory.Exists(rootPath)
            ? Directory.EnumerateFiles(rootPath, "*.tmp", SearchOption.AllDirectories).ToArray()
            : [];

    private static AssetDefinitionKey CreateKey() =>
        new(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);

    private sealed class ThrowingFaultInjector(
        FileSerializedAIAssetStoreWriteCheckpoint target)
        : IFileSerializedAIAssetStoreFaultInjector
    {
        public void OnCheckpoint(
            FileSerializedAIAssetStoreWriteCheckpoint checkpoint,
            string temporaryPath,
            string assetPath)
        {
            if (checkpoint == target)
            {
                throw new IOException($"Injected file-store failure at {checkpoint}.");
            }
        }
    }

    private sealed class CancellingFaultInjector(
        FileSerializedAIAssetStoreWriteCheckpoint target,
        CancellationTokenSource source)
        : IFileSerializedAIAssetStoreFaultInjector
    {
        public void OnCheckpoint(
            FileSerializedAIAssetStoreWriteCheckpoint checkpoint,
            string temporaryPath,
            string assetPath)
        {
            if (checkpoint == target)
            {
                source.Cancel();
            }
        }
    }

    private sealed class PausingFaultInjector(
        FileSerializedAIAssetStoreWriteCheckpoint target)
        : IFileSerializedAIAssetStoreFaultInjector, IDisposable
    {
        private readonly ManualResetEventSlim entered = new(false);
        private readonly ManualResetEventSlim release = new(false);

        public string? TemporaryPath { get; private set; }

        public void OnCheckpoint(
            FileSerializedAIAssetStoreWriteCheckpoint checkpoint,
            string temporaryPath,
            string assetPath)
        {
            if (checkpoint != target)
            {
                return;
            }

            TemporaryPath = temporaryPath;
            entered.Set();
            release.Wait();
        }

        public void WaitUntilEntered() => entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();

        public void Release() => release.Set();

        public void Dispose()
        {
            release.Set();
            entered.Dispose();
            release.Dispose();
        }
    }
}
