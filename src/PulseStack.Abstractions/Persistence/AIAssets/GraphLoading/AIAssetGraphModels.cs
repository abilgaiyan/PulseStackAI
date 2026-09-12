using System.Collections.ObjectModel;
using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

public sealed class AIAssetGraphNode
{
    public AIAssetGraphNode(AssetDefinitionKey definitionKey, IAsset asset)
    {
        AIAssetGraphContract.EnsureValidDefinitionKey(definitionKey, nameof(definitionKey));
        ArgumentNullException.ThrowIfNull(asset);

        if (AssetDefinitionKey.From(asset) != definitionKey)
        {
            throw new ArgumentException(
                "The asset identity must agree with the graph node definition key.",
                nameof(asset));
        }

        DefinitionKey = definitionKey;
        Asset = asset;
    }

    public AssetDefinitionKey DefinitionKey { get; }

    public IAsset Asset { get; }
}

public sealed class AIAssetGraphRelationship
{
    public AIAssetGraphRelationship(
        AssetDefinitionKey sourceKey,
        AssetReference targetReference,
        AIAssetGraphRelationshipClass relationshipClass,
        AIAssetGraphMaterializationAuthority materializationAuthority,
        AIAssetGraphBoundaryRole boundaryRole,
        bool? dependencyRequired,
        int localOrdinal,
        string authoredPath)
    {
        AIAssetGraphContract.EnsureValidDefinitionKey(sourceKey, nameof(sourceKey));
        AIAssetGraphContract.EnsureValidReference(targetReference, nameof(targetReference));
        AIAssetGraphContract.EnsureDefined(relationshipClass, nameof(relationshipClass));
        AIAssetGraphContract.EnsureDefined(materializationAuthority, nameof(materializationAuthority));
        AIAssetGraphContract.EnsureDefined(boundaryRole, nameof(boundaryRole));

        if (localOrdinal < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(localOrdinal));
        }

        if (string.IsNullOrWhiteSpace(authoredPath))
        {
            throw new ArgumentException("AuthoredPath cannot be null, empty, or whitespace.", nameof(authoredPath));
        }

        AIAssetGraphContract.EnsureRelationshipSemantics(
            relationshipClass,
            materializationAuthority,
            boundaryRole,
            dependencyRequired);

        SourceKey = sourceKey;
        TargetReference = targetReference;
        RelationshipClass = relationshipClass;
        MaterializationAuthority = materializationAuthority;
        BoundaryRole = boundaryRole;
        DependencyRequired = dependencyRequired;
        LocalOrdinal = localOrdinal;
        AuthoredPath = authoredPath;
    }

    public AssetDefinitionKey SourceKey { get; }

    public AssetReference TargetReference { get; }

    public AIAssetGraphRelationshipClass RelationshipClass { get; }

    public AIAssetGraphMaterializationAuthority MaterializationAuthority { get; }

    public AIAssetGraphBoundaryRole BoundaryRole { get; }

    public bool? DependencyRequired { get; }

    public int LocalOrdinal { get; }

    public string AuthoredPath { get; }
}

public sealed class AIAssetGraph
{
    public AIAssetGraph(
        AssetDefinitionKey rootKey,
        IEnumerable<AIAssetGraphNode> nodes,
        IEnumerable<AIAssetGraphRelationship> relationships)
    {
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(relationships);

        var nodeSnapshot = nodes.ToArray();
        var relationshipSnapshot = relationships.ToArray();

        if (nodeSnapshot.Any(static node => node is null))
        {
            throw new ArgumentException("Nodes cannot contain null entries.", nameof(nodes));
        }

        if (relationshipSnapshot.Any(static relationship => relationship is null))
        {
            throw new ArgumentException("Relationships cannot contain null entries.", nameof(relationships));
        }

        var duplicateNode = nodeSnapshot
            .GroupBy(static node => node.DefinitionKey)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateNode is not null)
        {
            throw new ArgumentException("Graph nodes must be unique by AssetDefinitionKey.", nameof(nodes));
        }

        var nodeKeys = nodeSnapshot.Select(static node => node.DefinitionKey).ToHashSet();
        if (!nodeKeys.Contains(rootKey))
        {
            throw new ArgumentException("The root key must identify exactly one graph node.", nameof(nodes));
        }

        var duplicateOrdinal = relationshipSnapshot
            .GroupBy(static relationship => (relationship.SourceKey, relationship.LocalOrdinal))
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateOrdinal is not null)
        {
            throw new ArgumentException(
                "LocalOrdinal must be unique for each relationship source key.",
                nameof(relationships));
        }

        foreach (var relationship in relationshipSnapshot)
        {
            if (!nodeKeys.Contains(relationship.SourceKey))
            {
                throw new ArgumentException(
                    "Every graph relationship source must identify a materialized graph node.",
                    nameof(relationships));
            }

            var targetKey = AssetDefinitionKey.From(relationship.TargetReference);
            if (relationship.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Required
                && !nodeKeys.Contains(targetKey))
            {
                throw new ArgumentException(
                    "Every required graph relationship target must identify a materialized graph node.",
                    nameof(relationships));
            }
        }

        Array.Sort(nodeSnapshot, static (left, right) =>
            AIAssetGraphContract.CompareDefinitionKeys(left.DefinitionKey, right.DefinitionKey));

        Array.Sort(relationshipSnapshot, static (left, right) =>
        {
            var sourceComparison = AIAssetGraphContract.CompareDefinitionKeys(left.SourceKey, right.SourceKey);
            return sourceComparison != 0
                ? sourceComparison
                : left.LocalOrdinal.CompareTo(right.LocalOrdinal);
        });

        RootKey = rootKey;
        Nodes = new ReadOnlyCollection<AIAssetGraphNode>(nodeSnapshot);
        Relationships = new ReadOnlyCollection<AIAssetGraphRelationship>(relationshipSnapshot);
    }

    public AssetDefinitionKey RootKey { get; }

    public IReadOnlyList<AIAssetGraphNode> Nodes { get; }

    public IReadOnlyList<AIAssetGraphRelationship> Relationships { get; }
}

internal static class AIAssetGraphContract
{
    private static readonly IReadOnlyDictionary<AssetType, int> TypeRanks =
        new Dictionary<AssetType, int>
        {
            [AssetType.Project] = 0,
            [AssetType.Library] = 1,
            [AssetType.Package] = 2,
            [AssetType.Workflow] = 3,
            [AssetType.Agent] = 4,
            [AssetType.Prompt] = 5,
            [AssetType.Tool] = 6,
            [AssetType.Knowledge] = 7,
            [AssetType.Memory] = 8,
            [AssetType.Policy] = 9,
            [AssetType.Model] = 10
        };

    public static void EnsureValidAggregateRootKey(AssetDefinitionKey key, string parameterName)
    {
        EnsureValidDefinitionKey(key, parameterName);

        if (key.Type is not (AssetType.Project or AssetType.Library or AssetType.Package))
        {
            throw new ArgumentException(
                "The graph root key must identify a Project, Library, or Package definition.",
                parameterName);
        }
    }

    public static void EnsureValidDefinitionKey(AssetDefinitionKey key, string parameterName)
    {
        if (!TypeRanks.ContainsKey(key.Type))
        {
            throw new ArgumentException(
                "The definition key must identify a supported schema-v1 AI Asset type.",
                parameterName);
        }

        if (key.Id.IsEmpty)
        {
            throw new ArgumentException("The definition key must contain a non-empty AssetId.", parameterName);
        }

        if (key.Version is null || string.IsNullOrWhiteSpace(key.Version.Value))
        {
            throw new ArgumentException("The definition key must contain a non-empty AssetVersion.", parameterName);
        }
    }

    public static void EnsureValidReference(AssetReference? reference, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(reference, parameterName);
        EnsureValidDefinitionKey(AssetDefinitionKey.From(reference), parameterName);

        if (reference.Urn is null || string.IsNullOrWhiteSpace(reference.Urn.Value))
        {
            throw new ArgumentException("The target reference must contain a non-empty AssetUrn.", parameterName);
        }
    }

    public static void EnsureDefined<TEnum>(TEnum value, string parameterName)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Unsupported graph contract enum value.");
        }
    }

    public static void EnsureRelationshipSemantics(
        AIAssetGraphRelationshipClass relationshipClass,
        AIAssetGraphMaterializationAuthority materializationAuthority,
        AIAssetGraphBoundaryRole boundaryRole,
        bool? dependencyRequired)
    {
        if (relationshipClass == AIAssetGraphRelationshipClass.ExplicitRequirement)
        {
            if (dependencyRequired is null)
            {
                throw new ArgumentException(
                    "DependencyRequired must be supplied for ExplicitRequirement relationships.",
                    nameof(dependencyRequired));
            }

            var expectedAuthority = dependencyRequired.Value
                ? AIAssetGraphMaterializationAuthority.Required
                : AIAssetGraphMaterializationAuthority.Excluded;
            if (materializationAuthority != expectedAuthority)
            {
                throw new ArgumentException(
                    "ExplicitRequirement materialization authority must agree with DependencyRequired.",
                    nameof(materializationAuthority));
            }

            if (boundaryRole is not (AIAssetGraphBoundaryRole.External or AIAssetGraphBoundaryRole.NotApplicable))
            {
                throw new ArgumentException(
                    "ExplicitRequirement boundary role must be External or NotApplicable.",
                    nameof(boundaryRole));
            }

            return;
        }

        if (dependencyRequired is not null)
        {
            throw new ArgumentException(
                "DependencyRequired is only valid for ExplicitRequirement relationships.",
                nameof(dependencyRequired));
        }

        if (materializationAuthority != AIAssetGraphMaterializationAuthority.Required)
        {
            throw new ArgumentException(
                "Non-dependency graph relationships are required materialization relationships.",
                nameof(materializationAuthority));
        }

        var validBoundary = relationshipClass switch
        {
            AIAssetGraphRelationshipClass.DistinguishedStructural =>
                boundaryRole == AIAssetGraphBoundaryRole.Structural,
            AIAssetGraphRelationshipClass.InternalOwnership or
            AIAssetGraphRelationshipClass.InternalMembership or
            AIAssetGraphRelationshipClass.InternalDistribution =>
                boundaryRole == AIAssetGraphBoundaryRole.Internal,
            AIAssetGraphRelationshipClass.DeclarativeReference =>
                boundaryRole == AIAssetGraphBoundaryRole.NotApplicable,
            _ => false
        };

        if (!validBoundary)
        {
            throw new ArgumentException(
                "BoundaryRole is inconsistent with RelationshipClass.",
                nameof(boundaryRole));
        }
    }

    public static int CompareDefinitionKeys(AssetDefinitionKey left, AssetDefinitionKey right)
    {
        var typeComparison = TypeRanks[left.Type].CompareTo(TypeRanks[right.Type]);
        if (typeComparison != 0)
        {
            return typeComparison;
        }

        var idComparison = string.CompareOrdinal(
            left.Id.Value.ToString("D").ToLowerInvariant(),
            right.Id.Value.ToString("D").ToLowerInvariant());
        if (idComparison != 0)
        {
            return idComparison;
        }

        return string.CompareOrdinal(left.Version.Value, right.Version.Value);
    }
}
