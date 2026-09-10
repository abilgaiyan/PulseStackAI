using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class WorkflowDocumentJsonReaderPrecedenceTests
{
    private const string DeserializerType =
        "PulseStack.Abstractions.Persistence.AIAssets.Serialization.AIAssetDocumentStrictJsonDeserializer";

    private const string Prefix =
        "{\"assetType\":\"workflow\",\"dependencies\":[],\"identity\":{\"id\":\"id\",\"urn\":\"urn:x\",\"version\":\"1\"},\"lifecycle\":\"draft\",\"metadata\":{\"author\":null,\"category\":null,\"description\":null,\"name\":\"n\",\"tags\":[]},\"references\":[],\"schemaVersion\":\"1.0\",\"steps\":[";

    [Fact]
    public void Deserialize_ShouldRejectDuplicateStepMemberBeforeUnknownStepDiscriminator()
    {
        var json = Prefix +
            "{\"kind\":\"future\",\"stepId\":\"a\",\"step\\u0049d\":\"b\"}]}";

        var exception = Failure(json);

        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.DuplicateMember);
        exception.MemberName.Should().Be("stepId");
    }

    [Fact]
    public void Deserialize_ShouldRejectDuplicateValueMemberBeforeUnknownValueDiscriminator()
    {
        var json = Prefix +
            "{\"items\":{\"kind\":\"future\",\"key\":\"a\",\"k\\u0065y\":\"b\"},\"kind\":\"loop\",\"name\":\"l\",\"step\":{\"agent\":{\"assetId\":\"a\",\"assetType\":\"agent\",\"urn\":\"u\",\"version\":\"1\"},\"kind\":\"run\",\"stepId\":\"s\"},\"stepId\":\"l\"}]}";

        var exception = Failure(json);

        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.DuplicateMember);
        exception.MemberName.Should().Be("key");
    }

    private static AIAssetDocumentCodecException Failure(string json)
    {
        var type = typeof(IAIAssetDocumentCodec).Assembly.GetType(DeserializerType, true)!;
        var method = type.GetMethod("Deserialize", BindingFlags.Static | BindingFlags.NonPublic,
            null, [typeof(string)], null)!;

        Action action = () => method.Invoke(null, [json]);

        return action.Should().Throw<TargetInvocationException>().Which.InnerException.Should()
            .BeOfType<AIAssetDocumentCodecException>().Which;
    }
}
