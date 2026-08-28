namespace PulseStack.Abstractions.Persistence.AIAssets.Validation;

public static class AIAssetDocumentValidationCodes
{
    public const string UnsupportedSchemaVersion = "AD100";
    public const string UnsupportedAssetType = "AD110";

    public const string InvalidIdentityId = "AD200";
    public const string MissingIdentityUrn = "AD210";
    public const string MissingIdentityVersion = "AD220";

    public const string MissingMetadataName = "AD300";
    public const string InvalidMetadataTag = "AD310";

    public const string UnsupportedReferenceAssetType = "AD400";
    public const string InvalidReferenceAssetId = "AD410";
    public const string MissingReferenceVersion = "AD420";
    public const string DuplicateReference = "AD430";

    public const string DuplicateDependency = "AD500";
}
