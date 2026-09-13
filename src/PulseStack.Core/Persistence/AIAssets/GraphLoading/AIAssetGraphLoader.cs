using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

namespace PulseStack.Core.Persistence.AIAssets.GraphLoading;

/// <summary>
/// Concrete B.7 orchestration for aggregate declarative graph loading.
/// </summary>
internal sealed class AIAssetGraphLoader : IAIAssetGraphLoader
{
    private readonly IPersistentAIAssetResolver resolver;

    internal AIAssetGraphLoader(IPersistentAIAssetResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        this.resolver = resolver;
    }

    public async ValueTask<AIAssetGraphLoadResult> LoadAsync(
        AssetDefinitionKey rootKey,
        CancellationToken cancellationToken = default)
    {
        // Frozen Decision 10 order: validate key/root support before observing cancellation.
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));
        cancellationToken.ThrowIfCancellationRequested();

        var completion = await AIAssetGraphSuccessfulOperationSnapshot.CompleteAsync(
            resolver,
            rootKey,
            cancellationToken).ConfigureAwait(false);

        if (completion.Failure is not null)
        {
            return completion.Failure;
        }

        var snapshot = completion.Success
            ?? throw new InvalidOperationException(
                "Graph loading completed without either a terminal failure or a success snapshot.");

        var graph = new AIAssetGraphResultBuilder(rootKey).Build(snapshot);
        return new AIAssetGraphLoadResult.Success(graph);
    }
}
