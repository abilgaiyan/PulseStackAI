using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Loads and reconstructs exactly one AI Asset definition by immutable definition key.
/// </summary>
public interface IAIAssetLoader
{
    ValueTask<AIAssetLoadResult> LoadAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default);
}
