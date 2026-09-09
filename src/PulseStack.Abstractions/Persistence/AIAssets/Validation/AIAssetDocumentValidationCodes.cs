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

    public const string MissingWorkflowStep = "AD740";
    public const string UnsupportedWorkflowStep = "AD750";
    public const string WorkflowStepTypeMismatch = "AD760";
    public const string InvalidWorkflowStepId = "AD770";
    public const string DuplicateWorkflowStepId = "AD780";
    public const string MissingWorkflowStepName = "AD790";
    public const string MissingRunAgentReference = "AD800";
    public const string InvalidRunAgentReference = "AD810";
    public const string InvalidRunAgentReferenceType = "AD820";
    public const string MissingWorkflowCondition = "AD830";
    public const string UnsupportedWorkflowCondition = "AD840";
    public const string WorkflowConditionTypeMismatch = "AD850";
    public const string MissingNamedConditionName = "AD860";
    public const string InvalidRetryMaxAttempts = "AD870";
    public const string MissingSwitchCase = "AD880";
    public const string InvalidSwitchCaseValue = "AD890";
    public const string DuplicateSwitchCaseValue = "AD900";
    public const string MissingWorkflowValue = "AD910";
    public const string UnsupportedWorkflowValue = "AD920";
    public const string WorkflowValueTypeMismatch = "AD930";
    public const string MissingContextItemKey = "AD940";
    public const string MissingWorkflowLiteral = "AD950";
    public const string UnsupportedWorkflowLiteral = "AD960";
    public const string WorkflowLiteralTypeMismatch = "AD970";
    public const string MissingWorkflowArrayItem = "AD980";
    public const string MissingWorkflowObjectProperty = "AD990";
    public const string MissingWorkflowObjectPropertyValue = "AD1000";
    public const string InvalidWorkflowObjectPropertyName = "AD1010";
    public const string DuplicateWorkflowObjectPropertyName = "AD1020";
    public const string NonCanonicalWorkflowObjectPropertyOrder = "AD1030";
    public const string ConflictingRunReferenceUrn = "AD1040";
    public const string WorkflowReferenceProjectionMismatch = "AD1050";
    public const string MissingWorkflowStringLiteralValue = "AD1060";

    public const string MissingEntryWorkflow = "AD1100";
    public const string InvalidEntryWorkflowType = "AD1110";
    public const string MissingOwnedAsset = "AD1120";
    public const string InvalidOwnedAssetType = "AD1130";
    public const string DuplicateOwnedAsset = "AD1140";
    public const string ConflictingOwnedAssetUrn = "AD1150";
    public const string EntryWorkflowNotOwned = "AD1160";
    public const string OwnedAssetDependencyOverlap = "AD1170";
    public const string ProjectReferenceProjectionMismatch = "AD1180";

    public const string MissingLibraryMember = "AD1190";
    public const string InvalidLibraryMemberType = "AD1200";
    public const string DuplicateLibraryMember = "AD1210";
    public const string ConflictingLibraryMemberUrn = "AD1220";
    public const string LibraryMemberDependencyOverlap = "AD1230";
    public const string LibraryReferenceProjectionMismatch = "AD1240";

    public const string EmptyPackageMembers = "AD1250";
    public const string MissingPackageMember = "AD1260";
    public const string DuplicatePackageMember = "AD1270";
    public const string ConflictingPackageMemberUrn = "AD1280";
    public const string DirectSelfPackageMember = "AD1290";
    public const string ConflictingPackageDependencyUrn = "AD1300";
    public const string ConflictingPackageDependencyRequiredness = "AD1310";
    public const string DirectSelfPackageDependency = "AD1320";
    public const string PackageMemberDependencyBoundaryContradiction = "AD1330";
    public const string PackageMemberDependencyUrnConflict = "AD1340";
    public const string PackageReferenceProjectionMismatch = "AD1350";
}
