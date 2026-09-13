using System.Collections.ObjectModel;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

namespace PulseStack.Core.Persistence.AIAssets.GraphLoading;

/// <summary>
/// Invocation-local recursive expansion authority for one aggregate declarative graph load.
/// B.4 owns recursive traversal and active-ancestry cycle semantics only; persistent resolution,
/// convergence, and identity authority remain delegated to <see cref="AIAssetGraphResolutionOperation"/>.
/// </summary>
internal sealed class AIAssetGraphExpansionOperation
{
    private readonly AssetDefinitionKey rootKey;
    private readonly AIAssetGraphResolutionOperation resolution;
    private readonly AIAssetGraphRelationshipEnumerator enumerator;
    private readonly HashSet<AssetDefinitionKey> completed = [];
    private readonly Dictionary<AssetDefinitionKey, int> activeStarts = [];

    private AIAssetGraphLoadResult? terminalFailure;

    internal AIAssetGraphExpansionOperation(
        IPersistentAIAssetResolver resolver,
        AssetDefinitionKey rootKey)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));

        this.rootKey = rootKey;
        resolution = new AIAssetGraphResolutionOperation(resolver, rootKey);
        enumerator = new AIAssetGraphRelationshipEnumerator();
    }

    internal AssetDefinitionKey RootKey => rootKey;

    internal AIAssetGraphLoadResult? TerminalFailure => terminalFailure ?? resolution.TerminalFailure;

    internal IReadOnlyList<AIAssetGraphNode> MaterializedNodes =>
        new ReadOnlyCollection<AIAssetGraphNode>(resolution.MaterializedNodes.ToArray());

    internal IReadOnlyList<AIAssetGraphRelationship> ObservedRelationships =>
        new ReadOnlyCollection<AIAssetGraphRelationship>(resolution.ObservedRelationships.ToArray());

    internal async ValueTask<AIAssetGraphLoadResult?> ExpandAsync(
        CancellationToken cancellationToken = default)
    {
        if (TerminalFailure is not null)
        {
            return TerminalFailure;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var rootFailure = await resolution.ResolveRootAsync(cancellationToken).ConfigureAwait(false);
        if (rootFailure is not null)
        {
            terminalFailure = rootFailure;
            return terminalFailure;
        }

        var rootAsset = GetMaterializedAsset(rootKey);
        var rootPath = new AIAssetGraphPath(rootKey, Array.Empty<AIAssetGraphPathSegment>());
        return await ExpandNodeAsync(rootAsset, rootPath, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<AIAssetGraphLoadResult?> ExpandNodeAsync(
        IAsset asset,
        AIAssetGraphPath path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (terminalFailure is not null)
        {
            return terminalFailure;
        }

        var sourceKey = AssetDefinitionKey.From(asset);
        if (completed.Contains(sourceKey))
        {
            return null;
        }

        if (!activeStarts.TryAdd(sourceKey, path.Segments.Count))
        {
            throw new InvalidOperationException(
                "B.4 expansion attempted to activate a definition that was already active without a closing relationship context.");
        }

        try
        {
            var relationships = enumerator.Enumerate(asset, cancellationToken);
            foreach (var relationship in relationships)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relationshipPath = Append(path, relationship);
                var relationshipFailure = await resolution
                    .ProcessRelationshipAsync(relationshipPath, relationship, cancellationToken)
                    .ConfigureAwait(false);

                if (relationshipFailure is not null)
                {
                    terminalFailure = relationshipFailure;
                    return terminalFailure;
                }

                if (relationship.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Excluded)
                {
                    continue;
                }

                if (relationship.MaterializationAuthority != AIAssetGraphMaterializationAuthority.Required)
                {
                    throw new InvalidOperationException(
                        "Graph relationship materialization authority is outside the frozen schema-v1 vocabulary.");
                }

                var targetKey = AssetDefinitionKey.From(relationship.TargetReference);
                if (activeStarts.TryGetValue(targetKey, out var cycleStartSegmentIndex))
                {
                    terminalFailure = new AIAssetGraphLoadResult.RequiredMaterializationCycle(
                        new AIAssetGraphRequiredMaterializationCycleContext(
                            rootKey,
                            relationshipPath,
                            relationship,
                            targetKey,
                            cycleStartSegmentIndex));
                    return terminalFailure;
                }

                if (completed.Contains(targetKey))
                {
                    continue;
                }

                var targetAsset = GetMaterializedAsset(targetKey);
                var nestedFailure = await ExpandNodeAsync(
                        targetAsset,
                        relationshipPath,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (nestedFailure is not null)
                {
                    terminalFailure = nestedFailure;
                    return terminalFailure;
                }
            }

            completed.Add(sourceKey);
            return null;
        }
        finally
        {
            activeStarts.Remove(sourceKey);
        }
    }

    private IAsset GetMaterializedAsset(AssetDefinitionKey key)
    {
        var node = resolution.MaterializedNodes.SingleOrDefault(node => node.DefinitionKey == key);
        return node?.Asset
            ?? throw new InvalidOperationException(
                "B.4 attempted to expand a required definition that B.3 had not successfully materialized.");
    }

    private AIAssetGraphPath Append(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship)
    {
        var segments = new AIAssetGraphPathSegment[path.Segments.Count + 1];
        for (var index = 0; index < path.Segments.Count; index++)
        {
            segments[index] = path.Segments[index];
        }

        segments[^1] = new AIAssetGraphPathSegment(relationship);
        return new AIAssetGraphPath(rootKey, segments);
    }
}
