using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;

namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Writes canonical serialized representations of exactly one AI Asset definition.
/// </summary>
public interface IAIAssetWriter
{
    ValueTask<AIAssetWriteResult> WriteAsync(
        AssetDefinitionKey key,
        AIAssetDocument document,
        CancellationToken cancellationToken = default);

    ValueTask<AIAssetWriteResult> WriteAsync(
        AssetDefinitionKey key,
        ReadOnlyMemory<byte> representation,
        CancellationToken cancellationToken = default);
}
