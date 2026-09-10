using PulseStack.Abstractions.Persistence.AIAssets.Documents;

namespace PulseStack.Abstractions.Persistence.AIAssets.Serialization;

/// <summary>
/// Canonical schema-v1 JSON codec for immutable AI Asset documents.
/// </summary>
public sealed class AIAssetDocumentCodec : IAIAssetDocumentCodec
{
    private const int StreamBufferSize = 81920;

    public byte[] Serialize(AIAssetDocument document) =>
        AIAssetDocumentCanonicalJsonSerializer.Serialize(document);

    public string SerializeToString(AIAssetDocument document) =>
        AIAssetDocumentCanonicalJsonSerializer.SerializeToString(document);

    public AIAssetDocument Deserialize(ReadOnlyMemory<byte> utf8Json) =>
        AIAssetDocumentStrictJsonDeserializer.Deserialize(utf8Json);

    public AIAssetDocument Deserialize(string json) =>
        AIAssetDocumentStrictJsonDeserializer.Deserialize(json);

    public async ValueTask SerializeAsync(
        AIAssetDocument document,
        Stream output,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(output);

        var bytes = Serialize(document);
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<AIAssetDocument> DeserializeAsync(
        Stream input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        using var buffer = new MemoryStream();
        var chunk = new byte[StreamBufferSize];
        while (true)
        {
            var read = await input.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            buffer.Write(chunk, 0, read);
        }

        return Deserialize(buffer.ToArray());
    }
}
