using System.Collections.ObjectModel;
using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

public sealed class AIAssetGraphPathSegment
{
    public AIAssetGraphPathSegment(AIAssetGraphRelationship relationship)
    {
        ArgumentNullException.ThrowIfNull(relationship);
        Relationship = relationship;
    }

    public AIAssetGraphRelationship Relationship { get; }

    public AssetDefinitionKey SourceKey => Relationship.SourceKey;
    public AssetReference TargetReference => Relationship.TargetReference;
    public AIAssetGraphRelationshipClass RelationshipClass => Relationship.RelationshipClass;
    public AIAssetGraphMaterializationAuthority MaterializationAuthority => Relationship.MaterializationAuthority;
    public AIAssetGraphBoundaryRole BoundaryRole => Relationship.BoundaryRole;
    public bool? DependencyRequired => Relationship.DependencyRequired;
    public int LocalOrdinal => Relationship.LocalOrdinal;
    public string AuthoredPath => Relationship.AuthoredPath;
}

public sealed class AIAssetGraphPath
{
    public AIAssetGraphPath(
        AssetDefinitionKey rootKey,
        IEnumerable<AIAssetGraphPathSegment> segments)
    {
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));
        ArgumentNullException.ThrowIfNull(segments);

        var snapshot = segments.ToArray();
        if (snapshot.Any(static segment => segment is null))
        {
            throw new ArgumentException("Path segments cannot contain null entries.", nameof(segments));
        }

        var expectedSource = rootKey;
        foreach (var segment in snapshot)
        {
            if (segment.SourceKey != expectedSource)
            {
                throw new ArgumentException(
                    "Path segments must form one contiguous authored relationship path from RootKey.",
                    nameof(segments));
            }

            expectedSource = AssetDefinitionKey.From(segment.TargetReference);
        }

        RootKey = rootKey;
        Segments = new ReadOnlyCollection<AIAssetGraphPathSegment>(snapshot);
    }

    public AssetDefinitionKey RootKey { get; }

    public IReadOnlyList<AIAssetGraphPathSegment> Segments { get; }
}
