using System.Collections.ObjectModel;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

namespace PulseStack.Core.Persistence.AIAssets.GraphLoading;

/// <summary>
/// B.6 success-only completion authority. The private constructor prevents arbitrary
/// collection snapshots from entering successful graph construction.
/// </summary>
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

    internal static async ValueTask<(AIAssetGraphLoadResult? Failure, AIAssetGraphSuccessfulOperationSnapshot? Success)> CompleteAsync(
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
            return (failure, null);
        }

        if (operation.TerminalFailure is not null)
        {
            throw new InvalidOperationException(
                "Frozen graph expansion reported successful completion while retaining a terminal graph failure.");
        }

        return (
            null,
            new AIAssetGraphSuccessfulOperationSnapshot(
                operation.RootKey,
                operation.MaterializedNodes.ToArray(),
                operation.ObservedRelationships.ToArray()));
    }
}
