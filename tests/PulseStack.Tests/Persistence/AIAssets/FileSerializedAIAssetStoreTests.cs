using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.Persistence.AIAssets.Storage;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class FileSerializedAIAssetStoreTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(),
        "PulseStack.Tests",
        nameof(FileSerializedAIAssetStoreTests),
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ReadAsync_AbsentKey_ShouldReturnNotFound()
    {
        var store = CreateStore();

        var result = await store.ReadAsync(CreateKey());

        result.Should().BeOfType<SerializedAIAssetReadResult.NotFound>();
    }

    [Fact]
    public async Task WriteAsync_FirstWrite_ShouldCreateAndReadExactBytesAcrossInstances()
    {
        var writer = CreateStore();
        var reader = CreateStore();
        var key = CreateKey();
        byte[] bytes = [1, 2, 3, 4];

        var write = await writer.WriteAsync(key, bytes);
        var read = await reader.ReadAsync(key);

        write.Should().Be(AIAssetWriteResult.Created);
        read.Should().BeOfType<SerializedAIAssetReadResult.Found>()
            .Which.Representation.ToArray().Should().Equal(bytes);
    }

    [Fact]
    public async Task WriteAsync_IdenticalExistingBytesAcrossInstances_ShouldReturnAlreadyPresent()
    {
        var first = CreateStore();
        var second = CreateStore();
        var key = CreateKey();
        byte[] bytes = [1, 2, 3];

        (await first.WriteAsync(key, bytes)).Should().Be(AIAssetWriteResult.Created);

        (await second.WriteAsync(key, bytes)).Should().Be(AIAssetWriteResult.AlreadyPresent);
    }

    [Fact]
    public async Task WriteAsync_DifferentExistingBytesAcrossInstances_ShouldReturnConflictWithoutMutation()
    {
        var first = CreateStore();
        var second = CreateStore();
        var key = CreateKey();

        (await first.WriteAsync(key, new byte[] { 1, 2, 3 })).Should().Be(AIAssetWriteResult.Created);

        (await second.WriteAsync(key, new byte[] { 4, 5, 6 })).Should().Be(AIAssetWriteResult.Conflict);
        var stored = (SerializedAIAssetReadResult.Found)await first.ReadAsync(key);
        stored.Representation.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task WriteAsync_ShouldOwnCallerBytesAfterReturn()
    {
        var store = CreateStore();
        var key = CreateKey();
        byte[] caller = [1, 2, 3];

        await store.WriteAsync(key, caller);
        caller[0] = 9;

        var stored = (SerializedAIAssetReadResult.Found)await store.ReadAsync(key);
        stored.Representation.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnDetachedBytes()
    {
        var store = CreateStore();
        var key = CreateKey();
        await store.WriteAsync(key, new byte[] { 1, 2, 3 });

        var first = (SerializedAIAssetReadResult.Found)await store.ReadAsync(key);
        var exposed = first.Representation.ToArray();
        exposed[0] = 9;

        var second = (SerializedAIAssetReadResult.Found)await store.ReadAsync(key);
        second.Representation.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ExactKeyPathMapping_ShouldKeepDistinctUnrestrictedVersionsDistinctAndInsideRoot()
    {
        var store = CreateStore();
        var id = AssetId.New();
        var firstKey = new AssetDefinitionKey(AssetType.Prompt, id, new AssetVersion("../alpha/β"));
        var secondKey = new AssetDefinitionKey(AssetType.Prompt, id, new AssetVersion("..\\alpha\\β"));

        (await store.WriteAsync(firstKey, new byte[] { 1 })).Should().Be(AIAssetWriteResult.Created);
        (await store.WriteAsync(secondKey, new byte[] { 2 })).Should().Be(AIAssetWriteResult.Created);

        ((SerializedAIAssetReadResult.Found)await store.ReadAsync(firstKey))
            .Representation.ToArray().Should().Equal(1);
        ((SerializedAIAssetReadResult.Found)await store.ReadAsync(secondKey))
            .Representation.ToArray().Should().Equal(2);

        Directory.GetFiles(rootPath, "*.asset", SearchOption.AllDirectories).Should().HaveCount(2);
    }

    [Fact]
    public async Task ExactKeyPathMapping_ShouldPreserveExactUtf16VersionIdentity()
    {
        await AssertVersionsRemainDistinctAsync("\uD800", "\uD801");
        await AssertVersionsRemainDistinctAsync("\uD800", "\uDC00");
        await AssertVersionsRemainDistinctAsync("\uD83D\uDE00", "\uD83D");
        await AssertVersionsRemainDistinctAsync("\uD83D\uDE00", "\uDE00");
        await AssertVersionsRemainDistinctAsync("é", "e\u0301");
    }

    [Fact]
    public async Task InvalidKey_ShouldWinOverAlreadyCancelledToken()
    {
        var store = CreateStore();
        var invalid = new AssetDefinitionKey(AssetType.Prompt, AssetId.Empty, AssetVersion.Initial);
        using var source = new CancellationTokenSource();
        source.Cancel();

        var read = async () => await store.ReadAsync(invalid, source.Token);
        var write = async () => await store.WriteAsync(invalid, new byte[] { 1 }, source.Token);

        await read.Should().ThrowAsync<ArgumentException>();
        await write.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AlreadyCancelledWrite_ShouldPublishNoAssetOrTemporaryFile()
    {
        var store = CreateStore();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var act = async () => await store.WriteAsync(CreateKey(), new byte[] { 1, 2, 3 }, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        Directory.GetFiles(rootPath, "*.asset", SearchOption.AllDirectories).Should().BeEmpty();
        Directory.GetFiles(rootPath, "*.tmp", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public async Task ConcurrentIdenticalWritesAcrossInstances_ShouldProduceOneCreatedAndRemainingAlreadyPresent()
    {
        var key = CreateKey();
        byte[] bytes = [1, 2, 3, 4];
        var stores = Enumerable.Range(0, 16).Select(_ => CreateStore()).ToArray();

        var results = await RunConcurrentlyAsync(
            stores.Select(store => (Func<ValueTask<AIAssetWriteResult>>)(() => store.WriteAsync(key, bytes))).ToArray());

        results.Count(result => result == AIAssetWriteResult.Created).Should().Be(1);
        results.Count(result => result == AIAssetWriteResult.AlreadyPresent).Should().Be(15);
        results.Should().NotContain(AIAssetWriteResult.Conflict);
    }

    [Fact]
    public async Task ConcurrentDifferentWritesAcrossInstances_ShouldPublishExactlyOneCompleteRepresentation()
    {
        var key = CreateKey();
        var candidates = Enumerable.Range(0, 16)
            .Select(index => Enumerable.Repeat((byte)(index + 1), 64 * 1024).ToArray())
            .ToArray();
        var stores = Enumerable.Range(0, candidates.Length).Select(_ => CreateStore()).ToArray();

        var operations = stores
            .Select((store, index) => (Func<ValueTask<AIAssetWriteResult>>)(() => store.WriteAsync(key, candidates[index])))
            .ToArray();
        var results = await RunConcurrentlyAsync(operations);

        results.Count(result => result == AIAssetWriteResult.Created).Should().Be(1);
        results.Count(result => result == AIAssetWriteResult.Conflict).Should().Be(15);

        var found = (SerializedAIAssetReadResult.Found)await CreateStore().ReadAsync(key);
        var published = found.Representation.ToArray();
        candidates.Any(candidate => candidate.AsSpan().SequenceEqual(published)).Should().BeTrue();
    }

    [Fact]
    public async Task DifferentKeys_ShouldPublishIndependently()
    {
        var store = CreateStore();
        var firstKey = CreateKey();
        var secondKey = CreateKey();

        var results = await RunConcurrentlyAsync(
            () => store.WriteAsync(firstKey, new byte[] { 1 }),
            () => store.WriteAsync(secondKey, new byte[] { 2 }));

        results.Should().OnlyContain(result => result == AIAssetWriteResult.Created);
        ((SerializedAIAssetReadResult.Found)await store.ReadAsync(firstKey)).Representation.ToArray().Should().Equal(1);
        ((SerializedAIAssetReadResult.Found)await store.ReadAsync(secondKey)).Representation.ToArray().Should().Equal(2);
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
        {
            Directory.Delete(rootPath, recursive: true);
        }
    }

    private async Task AssertVersionsRemainDistinctAsync(string firstVersion, string secondVersion)
    {
        var store = CreateStore();
        var id = AssetId.New();
        var firstKey = new AssetDefinitionKey(AssetType.Prompt, id, new AssetVersion(firstVersion));
        var secondKey = new AssetDefinitionKey(AssetType.Prompt, id, new AssetVersion(secondVersion));

        (await store.WriteAsync(firstKey, new byte[] { 1 })).Should().Be(AIAssetWriteResult.Created);
        (await store.WriteAsync(secondKey, new byte[] { 2 })).Should().Be(AIAssetWriteResult.Created);

        ((SerializedAIAssetReadResult.Found)await store.ReadAsync(firstKey))
            .Representation.ToArray().Should().Equal(1);
        ((SerializedAIAssetReadResult.Found)await store.ReadAsync(secondKey))
            .Representation.ToArray().Should().Equal(2);
    }

    private FileSerializedAIAssetStore CreateStore() => new(rootPath);

    private static AssetDefinitionKey CreateKey() =>
        new(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);

    private static async Task<AIAssetWriteResult[]> RunConcurrentlyAsync(
        params Func<ValueTask<AIAssetWriteResult>>[] operations)
    {
        using var ready = new SemaphoreSlim(0, operations.Length);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var workers = operations
            .Select(operation => Task.Run(async () =>
            {
                ready.Release();
                await start.Task.ConfigureAwait(false);
                return await operation().ConfigureAwait(false);
            }))
            .ToArray();

        for (var index = 0; index < operations.Length; index++)
        {
            await ready.WaitAsync().ConfigureAwait(false);
        }

        start.SetResult();
        return await Task.WhenAll(workers).ConfigureAwait(false);
    }
}
