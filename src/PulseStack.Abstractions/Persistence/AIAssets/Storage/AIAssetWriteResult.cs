namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Represents the semantic result of writing one exact serialized AI Asset entry.
/// </summary>
public enum AIAssetWriteResult
{
    Created,
    AlreadyPresent,
    Conflict
}
