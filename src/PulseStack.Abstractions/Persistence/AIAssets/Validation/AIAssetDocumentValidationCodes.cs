namespace PulseStack.Abstractions.Persistence.AIAssets.Validation;

public static class AIAssetDocumentValidationCodes
{
    public const string UnsupportedSchemaVersion = "AD100";
    public const string UnsupportedAssetType = "AD110";
    public const string AssetTypeMismatch = "AD120";
    public const string UnsupportedLifecycle = "AD130";

    public const string MissingIdentity = "AD190";
    public const string InvalidIdentityId = "AD200";
    public const string MissingIdentityUrn = "AD210";
    public const string MissingIdentityVersion = "AD220";

    public const string MissingMetadata = "AD290";
    public const string MissingMetadataName = "AD300";
    public const string InvalidMetadataTag = "AD310";

    public const string MissingReference = "AD390";
    public const string UnsupportedReferenceAssetType = "AD400";
    public const string InvalidReferenceAssetId = "AD410";
    public const string MissingReferenceVersion = "AD420";
    public const string DuplicateReference = "AD430";
    public const string MissingReferenceUrn = "AD440";

    public const string MissingDependency = "AD490";
    public const string MissingDependencyReference = "AD500";
    public const string DuplicateDependency = "AD510";

    public const string MissingPromptSystemInstructions = "AD600";
    public const string MissingModelProvider = "AD610";
    public const string MissingModelName = "AD620";
    public const string MissingToolDescription = "AD630";
    public const string MissingToolCategory = "AD640";
    public const string MissingKnowledgeDescription = "AD650";
    public const string MissingMemoryDescription = "AD660";
    public const string MissingPolicyDescription = "AD670";

    public const string MissingAgentGoal = "AD680";
    public const string MissingAgentRole = "AD690";
    public const string InvalidAgentResponsibility = "AD700";
    public const string InvalidAgentReferenceType = "AD710";
    public const string DuplicateAgentReference = "AD720";
    public const string AgentReferenceProjectionMismatch = "AD730";
}
