namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Provider-neutral limits applied to serialized AI Asset representations.
/// </summary>
public sealed record AIAssetStorageOptions
{
    public required long MaximumRepresentationSizeBytes { get; init; }
}
