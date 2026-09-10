namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Identifies MS-009.7 failures that are not represented by argument exceptions,
/// codec exceptions, semantic result values, or caller cancellation.
/// </summary>
public enum AIAssetStorageFailureCategory
{
    RepresentationTooLarge,
    DocumentValidation,
    KeyDocumentMismatch,
    NonCanonicalRepresentation,
    CompositionConfiguration,
    Mapping,
    ProviderStorage
}
