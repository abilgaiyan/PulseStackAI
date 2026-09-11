using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.Persistence.AIAssets.Storage;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public abstract class SerializedAIAssetStoreConformanceTests
{
    protected abstract IStoreNamespaceFixture CreateFixture();

    [Fact]
    public async Task Read_AbsentValidKey_ShouldReturnNotFound()
    {
        using var fixture = CreateFixture();
        var result = await fixture.CreateStore().ReadAsync(CreateKey());
        result.Should().BeOfType<SerializedAIAssetReadResult.NotFound>();
    }

    [Fact]
    public async Task ZeroLengthValue_ShouldRemainDistinctFromAbsence()
    {
        using var fixture = CreateFixture();
        var store = fixture.CreateStore();
        var key = CreateKey();

        (await store.WriteAsync(key, ReadOnlyMemory<byte>.Empty)).Should().Be(AIAssetWriteResult.Created);
        var result = await fixture.CreateStore().ReadAsync(key);

        var found = result.Should().BeOfType<SerializedAIAssetReadResult.Found>().Subject;
        found.Representation.Length.Should().Be(0);
    }

    [Fact]
    public async Task ArbitraryMalformedBytes_ShouldBeStoredOpaquelyAndExactly()
    {
        using var fixture = CreateFixture();
        var key = CreateKey();
        byte[] bytes = [0xFF, 0x00, 0xC0, 0xAF, 0x7B, 0x7D, 0xFE];

        (await fixture.CreateStore().WriteAsync(key, bytes)).Should().Be(AIAssetWriteResult.Created);
        var found = (SerializedAIAssetReadResult.Found)await fixture.CreateStore().ReadAsync(key);

        found.Representation.ToArray().Should().Equal(bytes);
    }

    [Fact]
    public async Task ImmutableWriteAlgebra_ShouldPreserveFirstBytesAcrossInstances()
    {
        using var fixture = CreateFixture();
        var first = fixture.CreateStore();
        var second = fixture.CreateStore();
        var key = CreateKey();
        byte[] original = [1, 2, 3];

        (await first.WriteAsync(key, original)).Should().Be(AIAssetWriteResult.Created);
        (await second.WriteAsync(key, original)).Should().Be(AIAssetWriteResult.AlreadyPresent);
        (await second.WriteAsync(key, new byte[] { 9, 8, 7 })).Should().Be(AIAssetWriteResult.Conflict);

        var found = (SerializedAIAssetReadResult.Found)await first.ReadAsync(key);
        found.Representation.ToArray().Should().Equal(original);
    }

    [Fact]
    public async Task Provider_ShouldOwnWriteInputAndReturnStableReadSnapshots()
    {
        using var fixture = CreateFixture();
        var store = fixture.CreateStore();
        var key = CreateKey();
        byte[] caller = [1, 2, 3, 4];

        (await store.WriteAsync(key, caller)).Should().Be(AIAssetWriteResult.Created);
        caller[0] = 99;

        var first = (SerializedAIAssetReadResult.Found)await store.ReadAsync(key);
        var retained = first.Representation.ToArray();
        var second = (SerializedAIAssetReadResult.Found)await fixture.CreateStore().ReadAsync(key);

        retained.Should().Equal(1, 2, 3, 4);
        second.Representation.ToArray().Should().Equal(1, 2, 3, 4);
        first.Representation.ToArray().Should().Equal(1, 2, 3, 4);
    }

    [Fact]
    public async Task ExactKeyComponents_ShouldAddressIndependentBindings()
    {
        using var fixture = CreateFixture();
        var id = AssetId.New();
        var otherId = AssetId.New();
        var version = new AssetVersion("1.0.0");

        var baseline = new AssetDefinitionKey(AssetType.Prompt, id, version);
        var differentType = new AssetDefinitionKey(AssetType.Tool, id, version);
        var differentId = new AssetDefinitionKey(AssetType.Prompt, otherId, version);
        var differentVersion = new AssetDefinitionKey(AssetType.Prompt, id, new AssetVersion("1.0.1"));

        var bindings = new[]
        {
            (baseline, new byte[] { 1 }),
            (differentType, new byte[] { 2 }),
            (differentId, new byte[] { 3 }),
            (differentVersion, new byte[] { 4 })
        };

        foreach (var (key, bytes) in bindings)
        {
            (await fixture.CreateStore().WriteAsync(key, bytes)).Should().Be(AIAssetWriteResult.Created);
        }

        foreach (var (key, bytes) in bindings)
        {
            var found = (SerializedAIAssetReadResult.Found)await fixture.CreateStore().ReadAsync(key);
            found.Representation.ToArray().Should().Equal(bytes);
        }
    }

    [Fact]
    public async Task ConcurrentIdenticalWritesAcrossInstances_ShouldLinearizeToOneCreated()
    {
        using var fixture = CreateFixture();
        var key = CreateKey();
        byte[] bytes = Enumerable.Repeat((byte)0x5A, 32 * 1024).ToArray();
        var stores = Enumerable.Range(0, 16).Select(_ => fixture.CreateStore()).ToArray();

        var results = await RunConcurrentlyAsync(
            stores.Select(store => (Func<ValueTask<AIAssetWriteResult>>)(() => store.WriteAsync(key, bytes))).ToArray());

        results.Count(result => result == AIAssetWriteResult.Created).Should().Be(1);
        results.Count(result => result == AIAssetWriteResult.AlreadyPresent).Should().Be(15);
        results.Should().NotContain(AIAssetWriteResult.Conflict);
    }

    [Fact]
    public async Task ConcurrentDifferentWritesAcrossInstances_ShouldPublishOneCompleteWinner()
    {
        using var fixture = CreateFixture();
        var key = CreateKey();
        var candidates = Enumerable.Range(1, 16)
            .Select(index => Enumerable.Repeat((byte)index, 64 * 1024).ToArray())
            .ToArray();
        var stores = Enumerable.Range(0, candidates.Length).Select(_ => fixture.CreateStore()).ToArray();

        var results = await RunConcurrentlyAsync(
            stores.Select((store, index) =>
                (Func<ValueTask<AIAssetWriteResult>>)(() => store.WriteAsync(key, candidates[index])))
                .ToArray());

        results.Count(result => result == AIAssetWriteResult.Created).Should().Be(1);
        results.Count(result => result == AIAssetWriteResult.Conflict).Should().Be(15);

        var winningIndex = Array.FindIndex(results, result => result == AIAssetWriteResult.Created);
        winningIndex.Should().BeGreaterThanOrEqualTo(0);
        var found = (SerializedAIAssetReadResult.Found)await fixture.CreateStore().ReadAsync(key);
        found.Representation.ToArray().Should().Equal(candidates[winningIndex]);
    }

    [Fact]
    public async Task OverlappingReads_ShouldObserveOnlyAbsenceOrTheCompleteWinningRepresentation()
    {
        using var fixture = CreateFixture();
        var key = CreateKey();
        var candidates = Enumerable.Range(1, 8)
            .Select(index => Enumerable.Repeat((byte)index, 128 * 1024).ToArray())
            .ToArray();
        var writers = candidates.Select(_ => fixture.CreateStore()).ToArray();
        var readers = Enumerable.Range(0, 16).Select(_ => fixture.CreateStore()).ToArray();

        using var ready = new SemaphoreSlim(0, writers.Length + readers.Length);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var writeTasks = writers.Select((store, index) => Task.Run(async () =>
        {
            ready.Release();
            await start.Task;
            return await store.WriteAsync(key, candidates[index]);
        })).ToArray();

        var readTasks = readers.Select(store => Task.Run(async () =>
        {
            ready.Release();
            await start.Task;
            var observations = new List<SerializedAIAssetReadResult>();
            for (var index = 0; index < 12; index++)
            {
                observations.Add(await store.ReadAsync(key));
                await Task.Yield();
            }

            return observations;
        })).ToArray();

        for (var index = 0; index < writers.Length + readers.Length; index++)
        {
            await ready.WaitAsync();
        }

        start.SetResult();
        var writeResults = await Task.WhenAll(writeTasks);
        var observations = (await Task.WhenAll(readTasks)).SelectMany(x => x).ToArray();

        writeResults.Count(result => result == AIAssetWriteResult.Created).Should().Be(1);
        writeResults.Count(result => result == AIAssetWriteResult.Conflict).Should().Be(candidates.Length - 1);

        var winningIndex = Array.FindIndex(writeResults, result => result == AIAssetWriteResult.Created);
        winningIndex.Should().BeGreaterThanOrEqualTo(0);
        var winningRepresentation = candidates[winningIndex];

        foreach (var observation in observations)
        {
            if (observation is SerializedAIAssetReadResult.NotFound)
            {
                continue;
            }

            var found = (SerializedAIAssetReadResult.Found)observation;
            found.Representation.ToArray().Should().Equal(winningRepresentation);
        }
    }

    [Fact]
    public async Task AlreadyCancelledWrite_ShouldReportCancellationAndPublishNothing()
    {
        using var fixture = CreateFixture();
        var key = CreateKey();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var act = async () => await fixture.CreateStore().WriteAsync(key, new byte[] { 1, 2, 3 }, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await fixture.CreateStore().ReadAsync(key)).Should().BeOfType<SerializedAIAssetReadResult.NotFound>();
    }

    private static AssetDefinitionKey CreateKey() =>
        new(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);

    private static async Task<AIAssetWriteResult[]> RunConcurrentlyAsync(
        params Func<ValueTask<AIAssetWriteResult>>[] operations)
    {
        using var ready = new SemaphoreSlim(0, operations.Length);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var workers = operations.Select(operation => Task.Run(async () =>
        {
            ready.Release();
            await start.Task;
            return await operation();
        })).ToArray();

        for (var index = 0; index < operations.Length; index++)
        {
            await ready.WaitAsync();
        }

        start.SetResult();
        return await Task.WhenAll(workers);
    }

    protected interface IStoreNamespaceFixture : IDisposable
    {
        ISerializedAIAssetStore CreateStore();
    }
}

public sealed class InMemorySerializedAIAssetStoreConformanceTests : SerializedAIAssetStoreConformanceTests
{
    protected override IStoreNamespaceFixture CreateFixture() => new Fixture();

    private sealed class Fixture : IStoreNamespaceFixture
    {
        private readonly InMemorySerializedAIAssetStoreNamespace storeNamespace = new();

        public ISerializedAIAssetStore CreateStore() => new InMemorySerializedAIAssetStore(storeNamespace);

        public void Dispose()
        {
        }
    }
}

public sealed class FileSerializedAIAssetStoreConformanceTests : SerializedAIAssetStoreConformanceTests
{
    protected override IStoreNamespaceFixture CreateFixture() => new Fixture();

    private sealed class Fixture : IStoreNamespaceFixture
    {
        private readonly string rootPath = Path.Combine(
            Path.GetTempPath(),
            "PulseStack.Tests",
            nameof(FileSerializedAIAssetStoreConformanceTests),
            Guid.NewGuid().ToString("N"));

        public ISerializedAIAssetStore CreateStore() => new FileSerializedAIAssetStore(rootPath);

        public void Dispose()
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }
}
