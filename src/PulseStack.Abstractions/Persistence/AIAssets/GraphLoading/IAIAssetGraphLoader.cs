using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

public interface IAIAssetGraphLoader
{
    ValueTask<AIAssetGraphLoadResult> LoadAsync(
        AssetDefinitionKey rootKey,
        CancellationToken cancellationToken = default);
}
