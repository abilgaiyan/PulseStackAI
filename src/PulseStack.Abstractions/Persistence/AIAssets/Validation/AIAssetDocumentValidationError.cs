namespace PulseStack.Abstractions.Persistence.AIAssets.Validation;

public sealed record AIAssetDocumentValidationError(
    string Code,
    string Message,
    string Path);
