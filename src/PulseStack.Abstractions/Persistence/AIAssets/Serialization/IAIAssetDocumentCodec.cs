using PulseStack.Abstractions.Persistence.AIAssets.Documents;

namespace PulseStack.Abstractions.Persistence.AIAssets.Serialization;

public interface IAIAssetDocumentCodec
{
    byte[] Serialize(AIAssetDocument document);

    string SerializeToString(AIAssetDocument document);

    AIAssetDocument Deserialize(ReadOnlyMemory<byte> utf8Json);

    AIAssetDocument Deserialize(string json);

    ValueTask SerializeAsync(
        AIAssetDocument document,
        Stream output,
        CancellationToken cancellationToken = default);

    ValueTask<AIAssetDocument> DeserializeAsync(
        Stream input,
        CancellationToken cancellationToken = default);
}
