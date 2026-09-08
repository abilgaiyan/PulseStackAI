namespace PulseStack.Abstractions.Persistence.AIAssets.Documents;

public sealed record AIAssetIdentityDocument
{
    public required string Id { get; init; }

    public required string Urn { get; init; }

    public required string Version { get; init; }
}
