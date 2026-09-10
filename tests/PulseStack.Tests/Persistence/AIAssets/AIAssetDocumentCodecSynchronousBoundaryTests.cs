using System.Text;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetDocumentCodecSynchronousBoundaryTests
{
    private readonly AIAssetDocumentCodec codec = new();

    [Fact]
    public void DeserializeStringAndBytes_ShouldReconstructTheSameDocument()
    {
        var document = Tool();
        var text = codec.SerializeToString(document);
        var bytes = codec.Serialize(document);

        codec.Deserialize(text).Should().BeEquivalentTo(document);
        codec.Deserialize(new ReadOnlyMemory<byte>(bytes)).Should().BeEquivalentTo(document);
    }

    [Fact]
    public void DeserializeBytes_ShouldAcceptExactlyOneLeadingTransportBom()
    {
        var canonical = codec.Serialize(Tool());
        var withBom = new byte[canonical.Length + 3];
        withBom[0] = 0xEF;
        withBom[1] = 0xBB;
        withBom[2] = 0xBF;
        canonical.CopyTo(withBom, 3);

        codec.Deserialize(withBom).Should().BeEquivalentTo(Tool());
    }

    [Fact]
    public void DeserializeString_ShouldTreatLeadingFeffAsJsonContentNotTransportBom()
    {
        var json = "\uFEFF" + codec.SerializeToString(Tool());

        Action action = () => codec.Deserialize(json);

        action.Should().Throw<AIAssetDocumentCodecException>()
            .Which.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidJson);
    }

    [Fact]
    public void DeserializeBytes_ShouldRejectMisplacedOrRepeatedBomAsInvalidJson()
    {
        var canonical = codec.Serialize(Tool());
        var repeated = new byte[canonical.Length + 6];
        repeated[0] = 0xEF; repeated[1] = 0xBB; repeated[2] = 0xBF;
        repeated[3] = 0xEF; repeated[4] = 0xBB; repeated[5] = 0xBF;
        canonical.CopyTo(repeated, 6);

        var whitespaceThenBom = Encoding.UTF8.GetBytes(" \uFEFF" + codec.SerializeToString(Tool()));

        Action repeatedAction = () => codec.Deserialize(repeated);
        Action misplacedAction = () => codec.Deserialize(whitespaceThenBom);

        repeatedAction.Should().Throw<AIAssetDocumentCodecException>()
            .Which.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidJson);
        misplacedAction.Should().Throw<AIAssetDocumentCodecException>()
            .Which.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidJson);
    }

    private static ToolAssetDocument Tool() => new(
        AIAssetSchemaVersion.V1,
        new AIAssetIdentityDocument { Id = "tool", Urn = "urn:tool", Version = "1" },
        new AIAssetMetadataDocument("Tool"),
        AIAssetLifecycleDocument.Published);
}
