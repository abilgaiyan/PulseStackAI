using System.Collections.ObjectModel;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

namespace PulseStack.Core.Persistence.AIAssets.GraphLoading;

/// <summary>
/// Internal completion gate for B.6. A success snapshot can only be minted after the frozen
/// expansion authority finishes without returning a graph-semantic failure. Predecessor
/// exceptions and caller cancellation propagate unchanged from ExpandAsync.
/// </summary>
internal sealed class AIAssetGraphOperationCompletion
{
    private AIAssetGraphOperationCompletion(
        AIAssetGraphLoadResult? failure,
        AIAssetGraphSuccessfulOperationSnapshot? success)
    {
        Failure = failure;
        Success = success;
    }

    internal AIAssetGraphLoadResult? Failure { get; }

    internal AIAssetGraphSuccessfulOperationSnapshot? Success { get; }

    internal static async ValueTask<AIAssetGraphOperationCompletion> CompleteAsync(
        IPersistentAIAssetResolver resolver,
        AssetDefinitionKey rootKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));

        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);
        var failure = await operation.ExpandAsync(cancellationToken).ConfigureAwait(false);
        if (failure is not null)
        {
            return new AIAssetGraphOperationCompletion(failure, null);
        }

        if (operation.TerminalFailure is not null)
        {
            throw new InvalidOperationException(
                "Frozen graph expansion reported successful completion while retaining a terminal graph failure.");
        }

        var success = AIAssetGraphSuccessfulOperationSnapshot.Create(
            operation.RootKey,
            operation.MaterializedNodes,
            operation.ObservedRelationships);
        return new AIAssetGraphOperationCompletion(null, success);
    }
}

internal sealed class AIAssetGraphSuccessfulOperationSnapshot
{
    private AIAssetGraphSuccessfulOperationSnapshot(
        AssetDefinitionKey rootKey,
        AIAssetGraphNode[] nodes,
        AIAssetGraphRelationship[] relationships)
    {
        RootKey = rootKey;
        MaterializedNodes = new ReadOnlyCollection<AIAssetGraphNode>(nodes);
        ObservedRelationships = new ReadOnlyCollection<AIAssetGraphRelationship>(relationships);
    }

    internal AssetDefinitionKey RootKey { get; }

    internal IReadOnlyList<AIAssetGraphNode> MaterializedNodes { get; }

    internal IReadOnlyList<AIAssetGraphRelationship> ObservedRelationships { get; }

    internal static AIAssetGraphSuccessfulOperationSnapshot Create(
        AssetDefinitionKey rootKey,
        IEnumerable<AIAssetGraphNode> materializedNodes,
        IEnumerable<AIAssetGraphRelationship> observedRelationships)
    {
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));
        ArgumentNullException.ThrowIfNull(materializedNodes);
        ArgumentNullException.ThrowIfNull(observedRelationships);

        return new AIAssetGraphSuccessfulOperationSnapshot(
            rootKey,
            materializedNodes.ToArray(),
            observedRelationships.ToArray());
    }
}
