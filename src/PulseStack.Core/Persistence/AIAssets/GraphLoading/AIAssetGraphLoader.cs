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
    private readonly AIAssetGraphLoaderExecutionHooks? executionHooks;

    public AIAssetGraphLoader(IPersistentAIAssetResolver resolver)
        : this(resolver, executionHooks: null)
    {
    }

    internal AIAssetGraphLoader(
        IPersistentAIAssetResolver resolver,
        AIAssetGraphLoaderExecutionHooks? executionHooks)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        this.resolver = resolver;
        this.executionHooks = executionHooks;
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
            cancellationToken,
            executionHooks?.AfterTerminalCommit).ConfigureAwait(false);

        if (completion.Failure is not null)
        {
            return completion.Failure;
        }

        var snapshot = completion.Success
            ?? throw new InvalidOperationException(
                "Graph loading completed without either a terminal failure or a success snapshot.");

        executionHooks?.BeforeGraphConstruction?.Invoke();
        var graph = new AIAssetGraphResultBuilder(rootKey).Build(snapshot);
        return new AIAssetGraphLoadResult.Success(graph);
    }
}

/// <summary>
/// Internal B.7 proof seam. Production composition does not require hooks; tests use them to
/// observe the exact terminal-commit boundary without changing public API or frozen semantics.
/// </summary>
internal sealed class AIAssetGraphLoaderExecutionHooks
{
    internal Action<AIAssetGraphLoadResult?>? AfterTerminalCommit { get; init; }

    internal Action? BeforeGraphConstruction { get; init; }
}
