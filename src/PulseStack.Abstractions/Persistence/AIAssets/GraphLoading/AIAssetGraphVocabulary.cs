namespace PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;

public enum AIAssetGraphRelationshipClass
{
    DistinguishedStructural,
    InternalOwnership,
    InternalMembership,
    InternalDistribution,
    DeclarativeReference,
    ExplicitRequirement
}

public enum AIAssetGraphMaterializationAuthority
{
    Required,
    Excluded
}

public enum AIAssetGraphBoundaryRole
{
    Structural,
    Internal,
    External,
    NotApplicable
}

public enum AIAssetGraphPredecessorSemanticOutcome
{
    DefinitionNotPublished,
    ReferenceMismatch
}

public enum AIAssetGraphReferenceIdentityConflictEvidence
{
    PersistentResolver,
    OperationLocalIdentity
}

public static class AIAssetGraphDiagnosticCodes
{
    public const string RootDefinitionUnavailable = "AAG001";
    public const string RequiredDefinitionUnavailable = "AAG002";
    public const string ReferenceIdentityConflict = "AAG003";
    public const string LineageIdentityConflict = "AAG004";
    public const string RequiredMaterializationCycle = "AAG005";
}
