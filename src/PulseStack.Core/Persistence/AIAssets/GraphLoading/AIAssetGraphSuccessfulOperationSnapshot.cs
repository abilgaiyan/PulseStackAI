using System.Collections.ObjectModel;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

namespace PulseStack.Core.Persistence.AIAssets.GraphLoading;

/// <summary>
/// B.6 success-only completion authority. B.7 binds snapshot minting to the frozen B.5
/// terminal-outcome authority so no success eligibility exists before terminal success.
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
        CancellationToken cancellationToken = default,
        Action<AIAssetGraphLoadResult?>? afterTerminalCommit = null)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));

        var coordinator = new AIAssetGraphFailureCoordinator(rootKey);
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        try
        {
            var observedFailure = await operation.ExpandAsync(cancellationToken).ConfigureAwait(false);
            if (observedFailure is not null)
            {
                coordinator.Observe(observedFailure);
                var committedFailure = coordinator.Commit(cancellationToken)
                    ?? throw new InvalidOperationException(
                        "A graph-semantic failure was observed but no terminal failure was committed.");
                afterTerminalCommit?.Invoke(committedFailure);
                return (committedFailure, null);
            }

            if (operation.TerminalFailure is not null)
            {
                coordinator.Observe(operation.TerminalFailure);
                var committedFailure = coordinator.Commit(cancellationToken)
                    ?? throw new InvalidOperationException(
                        "A retained graph-semantic failure was observed but no terminal failure was committed.");
                afterTerminalCommit?.Invoke(committedFailure);
                return (committedFailure, null);
            }

            // Commit is the final caller-cancellation boundary. A null return means no
            // semantic failure was selected and terminal success is now authoritative.
            var selectedFailure = coordinator.Commit(cancellationToken);
            if (selectedFailure is not null)
            {
                afterTerminalCommit?.Invoke(selectedFailure);
                return (selectedFailure, null);
            }

            afterTerminalCommit?.Invoke(null);
            return (
                null,
                new AIAssetGraphSuccessfulOperationSnapshot(
                    operation.RootKey,
                    operation.MaterializedNodes.ToArray(),
                    operation.ObservedRelationships.ToArray()));
        }
        catch (Exception exception)
        {
            var committedFailure = coordinator.PreservePredecessorFailure(exception, cancellationToken);
            if (committedFailure is not null)
            {
                return (committedFailure, null);
            }

            throw;
        }
    }
}
