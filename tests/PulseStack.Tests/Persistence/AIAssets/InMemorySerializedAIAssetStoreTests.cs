using System.Runtime.InteropServices;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Core.Persistence.AIAssets.Storage;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class InMemorySerializedAIAssetStoreTests
{
    [Fact]
    public async Task ReadAsync_AbsentKey_ShouldReturnNotFound()
    {
        var store = new InMemorySerializedAIAssetStore();

        var result = await store.ReadAsync(CreateKey());

        result.Should().BeOfType<SerializedAIAssetReadResult.NotFound>();
    }

    [Fact]
    public async Task WriteAsync_FirstWrite_ShouldCreateAndReadExactBytes()
    {
        var store = new InMemorySerializedAIAssetStore();
        var key = CreateKey();
        byte[] bytes = [1, 2, 3, 4];

        var write = await store.WriteAsync(key, bytes);
        var read = await store.ReadAsync(key);

        write.Should().Be(AIAssetWriteResult.Created);
        var found = read.Should().BeOfType<SerializedAIAssetReadResult.Found>().Subject;
        found.Representation.ToArray().Should().Equal(bytes);
    }

    [Fact]
    public async Task WriteAsync_ShouldOwnCallerBytes()
    {
        var store = new InMemorySerializedAIAssetStore();
        var key = CreateKey();
        byte[] caller = [1, 2, 3];

        await store.WriteAsync(key, caller);
        caller[0] = 9;

        var found = (SerializedAIAssetReadResult.Found)await store.ReadAsync(key);
        found.Representation.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ReadAsync_ShouldReturnDetachedProviderOwnedMemory()
    {
        var store = new InMemorySerializedAIAssetStore();
        var key = CreateKey();
        await store.WriteAsync(key, new byte[] { 1, 2, 3 });

        var first = (SerializedAIAssetReadResult.Found)await store.ReadAsync(key);
        MemoryMarshal.TryGetArray(first.Representation, out ArraySegment<byte> exposed).Should().BeTrue();
        exposed.Array.Should().NotBeNull();
        exposed.Array![exposed.Offset] = 9;

        var second = (SerializedAIAssetReadResult.Found)await store.ReadAsync(key);
        second.Representation.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task WriteAsync_IdenticalExistingBytes_ShouldReturnAlreadyPresent()
    {
        var store = new InMemorySerializedAIAssetStore();
        var key = CreateKey();
        await store.WriteAsync(key, new byte[] { 1, 2, 3 });

        var result = await store.WriteAsync(key, new byte[] { 1, 2, 3 });

        result.Should().Be(AIAssetWriteResult.AlreadyPresent);
    }

    [Fact]
    public async Task WriteAsync_DifferentExistingBytes_ShouldReturnConflictWithoutMutation()
    {
        var store = new InMemorySerializedAIAssetStore();
        var key = CreateKey();
        await store.WriteAsync(key, new byte[] { 1, 2, 3 });

        var result = await store.WriteAsync(key, new byte[] { 4, 5, 6 });
        var read = (SerializedAIAssetReadResult.Found)await store.ReadAsync(key);

        result.Should().Be(AIAssetWriteResult.Conflict);
        read.Representation.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task InvalidKey_ShouldWinOverAlreadyCancelledToken()
    {
        var store = new InMemorySerializedAIAssetStore();
        var invalid = new AssetDefinitionKey(AssetType.Prompt, AssetId.Empty, AssetVersion.Initial);
        using var source = new CancellationTokenSource();
        source.Cancel();

        var read = async () => await store.ReadAsync(invalid, source.Token);
        var write = async () => await store.WriteAsync(invalid, new byte[] { 1 }, source.Token);

        await read.Should().ThrowAsync<ArgumentException>();
        await write.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task AlreadyCancelledToken_ShouldPreventAnyMutation()
    {
        var store = new InMemorySerializedAIAssetStore();
        var key = CreateKey();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var act = async () => await store.WriteAsync(key, new byte[] { 1, 2, 3 }, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        (await store.ReadAsync(key)).Should().BeOfType<SerializedAIAssetReadResult.NotFound>();
    }

    [Fact]
    public async Task ConcurrentIdenticalWrites_ShouldProduceOneCreatedAndRemainingAlreadyPresent()
    {
        var store = new InMemorySerializedAIAssetStore();
        var key = CreateKey();
        byte[] bytes = [1, 2, 3, 4];

        var tasks = Enumerable.Range(0, 64)
            .Select(_ => store.WriteAsync(key, bytes).AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Count(result => result == AIAssetWriteResult.Created).Should().Be(1);
        results.Count(result => result == AIAssetWriteResult.AlreadyPresent).Should().Be(63);
        results.Should().NotContain(AIAssetWriteResult.Conflict);
    }

    [Fact]
    public async Task ConcurrentDifferentWrites_ShouldPublishExactlyOneRepresentation()
    {
        var store = new InMemorySerializedAIAssetStore();
        var key = CreateKey();
        var candidates = Enumerable.Range(0, 64)
            .Select(index => new byte[] { (byte)index, 42, 99 })
            .ToArray();

        var tasks = candidates
            .Select(bytes => store.WriteAsync(key, bytes).AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);
        var found = (SerializedAIAssetReadResult.Found)await store.ReadAsync(key);
        var published = found.Representation.ToArray();

        results.Count(result => result == AIAssetWriteResult.Created).Should().Be(1);
        results.Count(result => result == AIAssetWriteResult.Conflict).Should().Be(63);
        candidates.Any(candidate => candidate.AsSpan().SequenceEqual(published)).Should().BeTrue();
    }

    [Fact]
    public async Task DifferentKeys_ShouldRemainIndependent()
    {
        var store = new InMemorySerializedAIAssetStore();
        var firstKey = CreateKey();
        var secondKey = CreateKey();

        var firstWrite = store.WriteAsync(firstKey, new byte[] { 1 }).AsTask();
        var secondWrite = store.WriteAsync(secondKey, new byte[] { 2 }).AsTask();
        await Task.WhenAll(firstWrite, secondWrite);

        firstWrite.Result.Should().Be(AIAssetWriteResult.Created);
        secondWrite.Result.Should().Be(AIAssetWriteResult.Created);
        ((SerializedAIAssetReadResult.Found)await store.ReadAsync(firstKey)).Representation.ToArray().Should().Equal(1);
        ((SerializedAIAssetReadResult.Found)await store.ReadAsync(secondKey)).Representation.ToArray().Should().Equal(2);
    }

    private static AssetDefinitionKey CreateKey() =>
        new(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);
}
