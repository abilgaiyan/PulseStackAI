namespace PulseStack.Abstractions.Persistence.AIAssets.Validation;

public sealed record AIAssetDocumentValidationResult
{
    private readonly IReadOnlyList<AIAssetDocumentValidationError> errors;

    public AIAssetDocumentValidationResult(
        IEnumerable<AIAssetDocumentValidationError>? errors = null)
    {
        this.errors = Array.AsReadOnly(
            errors?.ToArray() ?? Array.Empty<AIAssetDocumentValidationError>());
    }

    public bool IsValid => errors.Count == 0;

    public IReadOnlyList<AIAssetDocumentValidationError> Errors => errors;

    public static AIAssetDocumentValidationResult Success()
        => new();

    public static AIAssetDocumentValidationResult Failure(
        params AIAssetDocumentValidationError[] errors)
        => new(errors);
}
