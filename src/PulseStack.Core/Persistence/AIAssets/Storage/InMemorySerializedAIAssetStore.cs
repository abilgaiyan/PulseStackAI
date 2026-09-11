using System.Collections.Concurrent;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;

namespace PulseStack.Core.Persistence.AIAssets.Storage;

/// <summary>
/// Stores exact serialized AI Asset representations in memory using immutable first-write semantics.
/// </summary>
public sealed class InMemorySerializedAIAssetStore : ISerializedAIAssetStore
{
    private readonly ConcurrentDictionary<AssetDefinitionKey, byte[]> storage = new();

    public ValueTask<SerializedAIAssetReadResult> ReadAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default)
    {
        AIAssetStorageContract.EnsureValidKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        if (!storage.TryGetValue(key, out var stored))
        {
            return ValueTask.FromResult<SerializedAIAssetReadResult>(
                new SerializedAIAssetReadResult.NotFound());
        }

        return ValueTask.FromResult<SerializedAIAssetReadResult>(
            new SerializedAIAssetReadResult.Found(stored.ToArray()));
    }

    public ValueTask<AIAssetWriteResult> WriteAsync(
        AssetDefinitionKey key,
        ReadOnlyMemory<byte> representation,
        CancellationToken cancellationToken = default)
    {
        AIAssetStorageContract.EnsureValidKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        var owned = representation.ToArray();
        cancellationToken.ThrowIfCancellationRequested();

        if (storage.TryAdd(key, owned))
        {
            return ValueTask.FromResult(AIAssetWriteResult.Created);
        }

        var stored = storage[key];
        return ValueTask.FromResult(
            stored.AsSpan().SequenceEqual(owned)
                ? AIAssetWriteResult.AlreadyPresent
                : AIAssetWriteResult.Conflict);
    }
}
