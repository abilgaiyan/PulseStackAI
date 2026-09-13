using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

namespace PulseStack.Core.Persistence.AIAssets.GraphLoading;

/// <summary>
/// B.6 success-only authority that validates completed operation state and constructs the
/// normalized, detached caller-facing graph. It does not select semantic failures.
/// </summary>
internal sealed class AIAssetGraphResultBuilder
{
    private readonly AssetDefinitionKey rootKey;

    internal AIAssetGraphResultBuilder(AssetDefinitionKey rootKey)
    {
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));
        this.rootKey = rootKey;
    }

    internal AIAssetGraph Build(
        IEnumerable<AIAssetGraphNode> materializedNodes,
        IEnumerable<AIAssetGraphRelationship> observedRelationships)
    {
        ArgumentNullException.ThrowIfNull(materializedNodes);
        ArgumentNullException.ThrowIfNull(observedRelationships);

        var nodes = materializedNodes.ToArray();
        var relationships = observedRelationships.ToArray();

        ValidateNodes(nodes);
        ValidateRelationships(nodes, relationships);

        // AIAssetGraph owns the frozen normalization and detached read-only snapshots.
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

            // DerivedProjection and OpaqueEnvelopeReference have no public graph-relationship
            // enum values; any future extension must not silently enter schema-v1 success output.
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
}
