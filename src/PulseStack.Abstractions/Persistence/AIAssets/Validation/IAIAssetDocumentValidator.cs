using PulseStack.Abstractions.Persistence.AIAssets.Documents;

namespace PulseStack.Abstractions.Persistence.AIAssets.Validation;

public interface IAIAssetDocumentValidator
{
    ValueTask<AIAssetDocumentValidationResult> ValidateAsync(
        AIAssetDocument document,
        CancellationToken cancellationToken = default);
}
