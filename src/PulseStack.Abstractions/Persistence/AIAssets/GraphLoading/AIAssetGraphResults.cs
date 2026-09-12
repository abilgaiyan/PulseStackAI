using PulseStack.Abstractions.Assets;

namespace PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

public abstract class AIAssetGraphFailureContext
{
    protected AIAssetGraphFailureContext(AssetDefinitionKey rootKey, AIAssetGraphPath canonicalPath)
    {
        AIAssetGraphContract.EnsureValidAggregateRootKey(rootKey, nameof(rootKey));
        ArgumentNullException.ThrowIfNull(canonicalPath);

        if (canonicalPath.RootKey != rootKey)
        {
            throw new ArgumentException(
                "CanonicalPath.RootKey must equal the requested graph RootKey.",
                nameof(canonicalPath));
        }

        RootKey = rootKey;
        CanonicalPath = canonicalPath;
    }

    public abstract string Code { get; }

    public AssetDefinitionKey RootKey { get; }

    public AIAssetGraphPath CanonicalPath { get; }
}

public sealed class AIAssetGraphRootDefinitionUnavailableContext : AIAssetGraphFailureContext
{
    public AIAssetGraphRootDefinitionUnavailableContext(AssetDefinitionKey rootKey)
        : base(rootKey, new AIAssetGraphPath(rootKey, Array.Empty<AIAssetGraphPathSegment>()))
    {
    }

    public override string Code => AIAssetGraphDiagnosticCodes.RootDefinitionUnavailable;
}

public abstract class AIAssetGraphRelationshipFailureContext : AIAssetGraphFailureContext
{
    protected AIAssetGraphRelationshipFailureContext(
        AssetDefinitionKey rootKey,
        AIAssetGraphPath canonicalPath,
        AIAssetGraphRelationship relationship)
        : base(rootKey, canonicalPath)
    {
        ArgumentNullException.ThrowIfNull(relationship);

        if (canonicalPath.Segments.Count == 0)
        {
            throw new ArgumentException(
                "A relationship-originated failure must have a non-empty canonical path.",
                nameof(canonicalPath));
        }

        var finalRelationship = canonicalPath.Segments[^1].Relationship;
        if (!SameOccurrence(finalRelationship, relationship))
        {
            throw new ArgumentException(
                "The final canonical path segment must identify the failing relationship occurrence.",
                nameof(relationship));
        }

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

    private static bool SameOccurrence(
        AIAssetGraphRelationship left,
        AIAssetGraphRelationship right) =>
        left.SourceKey == right.SourceKey
        && left.TargetReference == right.TargetReference
        && left.RelationshipClass == right.RelationshipClass
        && left.MaterializationAuthority == right.MaterializationAuthority
        && left.BoundaryRole == right.BoundaryRole
        && left.DependencyRequired == right.DependencyRequired
        && left.LocalOrdinal == right.LocalOrdinal
        && string.Equals(left.AuthoredPath, right.AuthoredPath, StringComparison.Ordinal);
}

public sealed class AIAssetGraphRequiredDefinitionUnavailableContext : AIAssetGraphRelationshipFailureContext
{
    public AIAssetGraphRequiredDefinitionUnavailableContext(
        AssetDefinitionKey rootKey,
        AIAssetGraphPath canonicalPath,
        AIAssetGraphRelationship relationship)
        : base(rootKey, canonicalPath, relationship)
    {
        if (relationship.MaterializationAuthority != AIAssetGraphMaterializationAuthority.Required)
        {
            throw new ArgumentException(
                "RequiredDefinitionUnavailable must originate from a required relationship.",
                nameof(relationship));
        }
    }

    public override string Code => AIAssetGraphDiagnosticCodes.RequiredDefinitionUnavailable;
}

public sealed class AIAssetGraphReferenceIdentityConflictContext : AIAssetGraphRelationshipFailureContext
{
    public AIAssetGraphReferenceIdentityConflictContext(
        AssetDefinitionKey rootKey,
        AIAssetGraphPath canonicalPath,
        AIAssetGraphRelationship relationship)
        : base(rootKey, canonicalPath, relationship)
    {
        if (relationship.MaterializationAuthority != AIAssetGraphMaterializationAuthority.Required)
        {
            throw new ArgumentException(
                "ReferenceIdentityConflict must originate from a required relationship.",
                nameof(relationship));
        }
    }

    public override string Code => AIAssetGraphDiagnosticCodes.ReferenceIdentityConflict;

    public AssetReference AssertedReference => TargetReference;
}

public sealed class AIAssetGraphLineageIdentityConflictContext : AIAssetGraphFailureContext
{
    public AIAssetGraphLineageIdentityConflictContext(
        AssetDefinitionKey rootKey,
        AIAssetGraphPath canonicalPath,
        AssetUrn conflictingUrn,
        AssetDefinitionKey establishedIdentity,
        AssetDefinitionKey conflictingIdentity)
        : base(rootKey, canonicalPath)
    {
        ArgumentNullException.ThrowIfNull(conflictingUrn);
        if (string.IsNullOrWhiteSpace(conflictingUrn.Value))
        {
            throw new ArgumentException("ConflictingUrn cannot be empty.", nameof(conflictingUrn));
        }

        AIAssetGraphContract.EnsureValidDefinitionKey(establishedIdentity, nameof(establishedIdentity));
        AIAssetGraphContract.EnsureValidDefinitionKey(conflictingIdentity, nameof(conflictingIdentity));

        if (establishedIdentity.Type == conflictingIdentity.Type
            && establishedIdentity.Id == conflictingIdentity.Id)
        {
            throw new ArgumentException(
                "LineageIdentityConflict requires incompatible type or AssetId lineage identity.",
                nameof(conflictingIdentity));
        }

        ConflictingUrn = conflictingUrn;
        EstablishedIdentity = establishedIdentity;
        ConflictingIdentity = conflictingIdentity;
    }

    public override string Code => AIAssetGraphDiagnosticCodes.LineageIdentityConflict;

    public AssetUrn ConflictingUrn { get; }
    public AssetDefinitionKey EstablishedIdentity { get; }
    public AssetDefinitionKey ConflictingIdentity { get; }
}

public sealed class AIAssetGraphRequiredMaterializationCycleContext : AIAssetGraphRelationshipFailureContext
{
    public AIAssetGraphRequiredMaterializationCycleContext(
        AssetDefinitionKey rootKey,
        AIAssetGraphPath canonicalPath,
        AIAssetGraphRelationship closingRelationship,
        AssetDefinitionKey cycleEntryKey,
        int cycleStartSegmentIndex)
        : base(rootKey, canonicalPath, closingRelationship)
    {
        if (closingRelationship.MaterializationAuthority != AIAssetGraphMaterializationAuthority.Required)
        {
            throw new ArgumentException(
                "RequiredMaterializationCycle must close through a required relationship.",
                nameof(closingRelationship));
        }

        AIAssetGraphContract.EnsureValidDefinitionKey(cycleEntryKey, nameof(cycleEntryKey));

        if (cycleStartSegmentIndex < 0 || cycleStartSegmentIndex >= canonicalPath.Segments.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(cycleStartSegmentIndex));
        }

        var closingTarget = AssetDefinitionKey.From(closingRelationship.TargetReference);
        if (closingTarget != cycleEntryKey)
        {
            throw new ArgumentException(
                "CycleEntryKey must equal the target of the closing relationship.",
                nameof(cycleEntryKey));
        }

        var cycleStartSource = canonicalPath.Segments[cycleStartSegmentIndex].SourceKey;
        if (cycleStartSource != cycleEntryKey)
        {
            throw new ArgumentException(
                "CycleStartSegmentIndex must identify the path position where expansion from CycleEntryKey begins.",
                nameof(cycleStartSegmentIndex));
        }

        CycleEntryKey = cycleEntryKey;
        CycleStartSegmentIndex = cycleStartSegmentIndex;
    }

    public override string Code => AIAssetGraphDiagnosticCodes.RequiredMaterializationCycle;

    public AssetDefinitionKey CycleEntryKey { get; }
    public int CycleStartSegmentIndex { get; }
}

public abstract record AIAssetGraphLoadResult
{
    private AIAssetGraphLoadResult() { }

    public sealed record Success : AIAssetGraphLoadResult
    {
        public Success(AIAssetGraph graph)
        {
            ArgumentNullException.ThrowIfNull(graph);
            Graph = graph;
        }

        public AIAssetGraph Graph { get; }
    }

    public sealed record RootDefinitionUnavailable : AIAssetGraphLoadResult
    {
        public RootDefinitionUnavailable(AIAssetGraphRootDefinitionUnavailableContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Context = context;
        }

        public AIAssetGraphRootDefinitionUnavailableContext Context { get; }
    }

    public sealed record RequiredDefinitionUnavailable : AIAssetGraphLoadResult
    {
        public RequiredDefinitionUnavailable(AIAssetGraphRequiredDefinitionUnavailableContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Context = context;
        }

        public AIAssetGraphRequiredDefinitionUnavailableContext Context { get; }
    }

    public sealed record ReferenceIdentityConflict : AIAssetGraphLoadResult
    {
        public ReferenceIdentityConflict(AIAssetGraphReferenceIdentityConflictContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Context = context;
        }

        public AIAssetGraphReferenceIdentityConflictContext Context { get; }
    }

    public sealed record LineageIdentityConflict : AIAssetGraphLoadResult
    {
        public LineageIdentityConflict(AIAssetGraphLineageIdentityConflictContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Context = context;
        }

        public AIAssetGraphLineageIdentityConflictContext Context { get; }
    }

    public sealed record RequiredMaterializationCycle : AIAssetGraphLoadResult
    {
        public RequiredMaterializationCycle(AIAssetGraphRequiredMaterializationCycleContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            Context = context;
        }

        public AIAssetGraphRequiredMaterializationCycleContext Context { get; }
    }
}
