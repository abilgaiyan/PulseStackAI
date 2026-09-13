using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

namespace PulseStack.Core.Persistence.AIAssets.GraphLoading;

/// <summary>
/// B.6 success-only authority that validates a completion-bound operation snapshot and constructs
/// the normalized, detached caller-facing graph. It does not select semantic failures.
/// </summary>
internal sealed class AIAssetGraphResultBuilder
{
    private readonly AssetDefinitionKey rootKey;
    private readonly AIAssetGraphRelationshipEnumerator relationshipEnumerator = new();

    internal AIAssetGraphResultBuilder(AssetDefinitionKey rootKey)
    {
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));
        this.rootKey = rootKey;
    }

    internal AIAssetGraph Build(AIAssetGraphSuccessfulOperationSnapshot successfulOperation)
    {
        ArgumentNullException.ThrowIfNull(successfulOperation);

        if (successfulOperation.RootKey != rootKey)
        {
            throw new InvalidOperationException(
                "The successful operation snapshot root must match the graph result builder root authority.");
        }

        var nodes = successfulOperation.MaterializedNodes.ToArray();
        var relationships = successfulOperation.ObservedRelationships.ToArray();

        ValidateNodes(nodes);
        ValidateRelationships(nodes, relationships);
        ValidateAuthoredRelationshipCompleteness(nodes, relationships);
        ValidateRequiredReachability(nodes, relationships);

        return new AIAssetGraph(rootKey, nodes, relationships);
    }

    private void ValidateNodes(IReadOnlyList<AIAssetGraphNode> nodes)
    {
        if (nodes.Any(static node => node is null))
        {
            throw new InvalidOperationException("Completed B.6 operation state contains a null materialized node.");
        }

        var duplicate = nodes
            .GroupBy(static node => node.DefinitionKey)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                "Completed B.6 operation state contains more than one node for an AssetDefinitionKey.");
        }

        if (nodes.Count(node => node.DefinitionKey == rootKey) != 1)
        {
            throw new InvalidOperationException(
                "Completed B.6 operation state must contain the exact aggregate root exactly once.");
        }

        foreach (var node in nodes)
        {
            if (AssetDefinitionKey.From(node.Asset) != node.DefinitionKey)
            {
                throw new InvalidOperationException(
                    "Completed B.6 operation state contains a node whose asset identity disagrees with its definition key.");
            }
        }
    }

    private static void ValidateRelationships(
        IReadOnlyList<AIAssetGraphNode> nodes,
        IReadOnlyList<AIAssetGraphRelationship> relationships)
    {
        if (relationships.Any(static relationship => relationship is null))
        {
            throw new InvalidOperationException("Completed B.6 operation state contains a null graph relationship.");
        }

        var nodeByKey = nodes.ToDictionary(static node => node.DefinitionKey);
        var duplicateOrdinal = relationships
            .GroupBy(static relationship => (relationship.SourceKey, relationship.LocalOrdinal))
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateOrdinal is not null)
        {
            throw new InvalidOperationException(
                "Completed B.6 operation state contains duplicate source-local relationship ordinals.");
        }

        foreach (var relationship in relationships)
        {
            if (!Enum.IsDefined(relationship.RelationshipClass))
            {
                throw new InvalidOperationException("Completed B.6 operation state contains an unsupported relationship class.");
            }

            if (relationship.RelationshipClass is not (
                AIAssetGraphRelationshipClass.DistinguishedStructural
                or AIAssetGraphRelationshipClass.InternalOwnership
                or AIAssetGraphRelationshipClass.InternalMembership
                or AIAssetGraphRelationshipClass.InternalDistribution
                or AIAssetGraphRelationshipClass.DeclarativeReference
                or AIAssetGraphRelationshipClass.ExplicitRequirement))
            {
                throw new InvalidOperationException(
                    "Completed B.6 operation state contains a non-authoritative graph relationship class.");
            }

            if (!nodeByKey.ContainsKey(relationship.SourceKey))
            {
                throw new InvalidOperationException(
                    "Every completed graph relationship source must be materialized.");
            }

            var targetKey = AssetDefinitionKey.From(relationship.TargetReference);
            if (!nodeByKey.TryGetValue(targetKey, out var targetNode))
            {
                if (relationship.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Required)
                {
                    throw new InvalidOperationException(
                        "Every required completed graph relationship target must be materialized exactly once.");
                }

                if (relationship.MaterializationAuthority != AIAssetGraphMaterializationAuthority.Excluded)
                {
                    throw new InvalidOperationException(
                        "An absent graph target is valid only for an excluded optional relationship.");
                }

                continue;
            }

            if (targetNode.Asset.Urn != relationship.TargetReference.Urn)
            {
                throw new InvalidOperationException(
                    "A materialized graph target must agree with the authored target URN assertion.");
            }
        }
    }

    private void ValidateAuthoredRelationshipCompleteness(
        IReadOnlyList<AIAssetGraphNode> nodes,
        IReadOnlyList<AIAssetGraphRelationship> relationships)
    {
        var actualBySource = relationships
            .GroupBy(static relationship => relationship.SourceKey)
            .ToDictionary(
                static group => group.Key,
                static group => group.OrderBy(static relationship => relationship.LocalOrdinal).ToArray());

        foreach (var node in nodes)
        {
            var expected = relationshipEnumerator.Enumerate(node.Asset);
            actualBySource.TryGetValue(node.DefinitionKey, out var actual);
            actual ??= Array.Empty<AIAssetGraphRelationship>();

            if (expected.Count != actual.Length)
            {
                throw new InvalidOperationException(
                    "Completed B.6 operation state does not contain every authoritative relationship authored by a materialized source definition.");
            }

            for (var index = 0; index < expected.Count; index++)
            {
                if (!RelationshipEquivalent(expected[index], actual[index]))
                {
                    throw new InvalidOperationException(
                        "Completed B.6 operation state does not exactly preserve the authoritative authored relationship occurrences for a materialized source definition.");
                }
            }
        }
    }

    private void ValidateRequiredReachability(
        IReadOnlyList<AIAssetGraphNode> nodes,
        IReadOnlyList<AIAssetGraphRelationship> relationships)
    {
        var requiredBySource = relationships
            .Where(static relationship =>
                relationship.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Required)
            .GroupBy(static relationship => relationship.SourceKey)
            .ToDictionary(static group => group.Key, static group => group.ToArray());

        var reached = new HashSet<AssetDefinitionKey> { rootKey };
        var queue = new Queue<AssetDefinitionKey>();
        queue.Enqueue(rootKey);

        while (queue.Count > 0)
        {
            var sourceKey = queue.Dequeue();
            if (!requiredBySource.TryGetValue(sourceKey, out var outgoing))
            {
                continue;
            }

            foreach (var relationship in outgoing)
            {
                var targetKey = AssetDefinitionKey.From(relationship.TargetReference);
                if (reached.Add(targetKey))
                {
                    queue.Enqueue(targetKey);
                }
            }
        }

        if (reached.Count != nodes.Count
            || nodes.Any(node => !reached.Contains(node.DefinitionKey)))
        {
            throw new InvalidOperationException(
                "Completed B.6 operation state contains a materialized node outside the root required-materialization closure.");
        }
    }

    private static bool RelationshipEquivalent(
        AIAssetGraphRelationship left,
        AIAssetGraphRelationship right) =>
        left.SourceKey == right.SourceKey
        && left.TargetReference.Type == right.TargetReference.Type
        && left.TargetReference.Id == right.TargetReference.Id
        && left.TargetReference.Urn == right.TargetReference.Urn
        && left.TargetReference.Version == right.TargetReference.Version
        && left.RelationshipClass == right.RelationshipClass
        && left.MaterializationAuthority == right.MaterializationAuthority
        && left.BoundaryRole == right.BoundaryRole
        && left.DependencyRequired == right.DependencyRequired
        && left.LocalOrdinal == right.LocalOrdinal
        && string.Equals(left.AuthoredPath, right.AuthoredPath, StringComparison.Ordinal);
}
