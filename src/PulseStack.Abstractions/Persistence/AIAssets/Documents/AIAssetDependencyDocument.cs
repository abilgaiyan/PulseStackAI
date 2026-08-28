namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public sealed record AIAssetDependencyDocument
{
    public required AIAssetReferenceDocument Reference { get; init; }

    public bool Required { get; init; } = true;
}
