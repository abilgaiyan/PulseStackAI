using PulseStack.Abstractions.Persistence.AIAssets.Validation;

namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

public sealed class AIAssetStorageException : Exception
{
    public AIAssetStorageException(
        AIAssetStorageFailureCategory category,
        string message,
        AIAssetStorageDiagnosticContext? context = null,
        AIAssetDocumentValidationResult? validationResult = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Category = category;
        Context = context;
        ValidationResult = validationResult;
    }

    public AIAssetStorageFailureCategory Category { get; }

    public AIAssetStorageDiagnosticContext? Context { get; }

    /// <summary>
    /// Preserves the complete ordered validation result when the failure category is
    /// <see cref="AIAssetStorageFailureCategory.DocumentValidation"/>.
    /// </summary>
    public AIAssetDocumentValidationResult? ValidationResult { get; }
}
