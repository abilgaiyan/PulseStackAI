namespace PulseStack.Abstractions.Persistence.AIAssets.Serialization;

public enum AIAssetDocumentCodecFailureReason
{
    InvalidEncoding,
    InvalidJson,
    UnknownMember,
    DuplicateMember,
    MissingRequiredMember,
    InvalidTokenKind,
    MalformedSchemaVersion,
    UnsupportedSchemaVersion,
    UnknownDiscriminator,
    UnsupportedDiscriminator,
    InvalidScalarToken,
    InvalidScalarValue,
    UnsupportedDocumentType,
    DiscriminatorMismatch,
    InvalidUnicode,
    UnrepresentableDocument
}
