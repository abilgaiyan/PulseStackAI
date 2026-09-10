using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetDocumentCodecUnicodeConformanceTests
{
    [Theory]
    [InlineData("\\uD800")]
    [InlineData("\\uDC00")]
    public void Deserialize_ShouldClassifyEscapedUnpairedSurrogateAsInvalidUnicode(string invalidEscape)
    {
        IAIAssetDocumentCodec codec = new AIAssetDocumentCodec();
        var json = "{\"assetType\":\"tool\",\"dependencies\":[],\"identity\":{\"id\":\"tool\",\"urn\":\"urn:tool\",\"version\":\"1\"},\"lifecycle\":\"published\",\"metadata\":{\"author\":null,\"category\":null,\"description\":null,\"name\":\"" + invalidEscape + "\",\"tags\":[]},\"references\":[],\"schemaVersion\":\"1.0\"}";

        Action action = () => codec.Deserialize(json);

        var failure = action.Should().Throw<AIAssetDocumentCodecException>().Which;
        failure.Operation.Should().Be(AIAssetDocumentCodecOperation.Deserialization);
        failure.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidUnicode);
    }
}
