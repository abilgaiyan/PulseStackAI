namespace PulseStack.Abstractions.Persistence.AIAssets.Storage;

/// <summary>
/// Represents the semantic result of reading one exact serialized AI Asset entry.
/// </summary>
public abstract record SerializedAIAssetReadResult
{
    private SerializedAIAssetReadResult()
    {
    }

    public sealed record Found(ReadOnlyMemory<byte> Representation) : SerializedAIAssetReadResult;

    public sealed record NotFound : SerializedAIAssetReadResult;
}
