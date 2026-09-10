using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Stores and retrieves one exact serialized AI Asset representation by immutable definition key.
/// </summary>
public interface ISerializedAIAssetStore
{
    ValueTask<SerializedAIAssetReadResult> ReadAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default);

    ValueTask<AIAssetWriteResult> WriteAsync(
        AssetDefinitionKey key,
        ReadOnlyMemory<byte> representation,
        CancellationToken cancellationToken = default);
}
