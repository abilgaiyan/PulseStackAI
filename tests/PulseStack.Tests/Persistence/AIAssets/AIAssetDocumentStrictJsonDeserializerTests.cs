using System.Reflection;
using System.Text;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetDocumentStrictJsonDeserializerTests
{
    private const string AuthorityType =
        "PulseStack.Abstractions.Persistence.AIAssets.Serialization.AIAssetDocumentStrictJsonDeserializer";

    private const string ValidTool =
        "{\"assetType\":\"tool\",\"dependencies\":[],\"identity\":{},\"lifecycle\":\"draft\",\"metadata\":{},\"references\":[],\"schemaVersion\":\"1.0\"}";

    [Fact]
    public void Parse_ShouldAcceptOneWholeArtifactWithWhitespaceAndOptionalByteBom()
    {
        InvokeString(" \n" + ValidTool + "\t ").Should().NotBeNull();

        var bytes = Encoding.UTF8.GetBytes(ValidTool + "\n");
        var withBom = new byte[bytes.Length + 3];
        withBom[0] = 0xEF; withBom[1] = 0xBB; withBom[2] = 0xBF;
        bytes.CopyTo(withBom, 3);

        InvokeBytes(withBom).Should().NotBeNull();
    }

    [Fact]
    public void Parse_ShouldRejectMalformedUtf8AsInvalidEncoding()
    {
        var exception = FailureBytes([0x7B, 0x22, 0xC3, 0x28, 0x22, 0x7D]);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidEncoding);
    }

    [Fact]
    public void Parse_ShouldRejectMalformedOrMultipleArtifactsAsInvalidJson()
    {
        FailureString(ValidTool + ValidTool).FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidJson);
        FailureString("\uFEFF" + ValidTool).FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidJson);

        var twiceBom = Encoding.UTF8.GetBytes("\uFEFF\uFEFF" + ValidTool);
        FailureBytes(twiceBom).FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidJson);
    }

    [Fact]
    public void Parse_ShouldRejectDecodedDuplicateRootMembers()
    {
        var json = ValidTool.Replace(
            "\"assetType\":\"tool\"",
            "\"assetType\":\"tool\",\"asset\\u0054ype\":\"tool\"");

        var exception = FailureString(json);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.DuplicateMember);
        exception.MemberName.Should().Be("assetType");
    }

    [Fact]
    public void Parse_ShouldRejectUnknownRootMemberAfterRootDispatch()
    {
        var json = ValidTool[..^1] + ",\"future\":true}";
        var exception = FailureString(json);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.UnknownMember);
        exception.MemberName.Should().Be("future");
    }

    [Fact]
    public void Parse_ShouldClassifyRequiredDiscriminatorAndSchemaVersionFailures()
    {
        FailureString(ValidTool.Replace("\"assetType\":\"tool\",", ""))
            .FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.MissingRequiredMember);
        FailureString(ValidTool.Replace("\"assetType\":\"tool\"", "\"assetType\":1"))
            .FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidTokenKind);
        FailureString(ValidTool.Replace("\"schemaVersion\":\"1.0\"", "\"schemaVersion\":\"01.0\""))
            .FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.MalformedSchemaVersion);
        FailureString(ValidTool.Replace("\"schemaVersion\":\"1.0\"", "\"schemaVersion\":\"1.1\""))
            .FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.UnsupportedSchemaVersion);
    }

    [Fact]
    public void Parse_ShouldReuseFrozenRootDiscriminatorClassification()
    {
        FailureString(ValidTool.Replace("\"tool\"", "\"provider\"", StringComparison.Ordinal))
            .FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.UnsupportedDiscriminator);
        FailureString(ValidTool.Replace("\"tool\"", "\"Tool\"", StringComparison.Ordinal))
            .FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.UnknownDiscriminator);
    }

    [Fact]
    public void ParseString_ShouldRejectUnpairedUtf16AsInvalidUnicode()
    {
        var invalid = ValidTool.Replace("tool", new string(['\uD800']), StringComparison.Ordinal);
        FailureString(invalid).FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidUnicode);
    }

    private static object InvokeString(string json) => Invoke(GetMethod(typeof(string)), json);

    private static object InvokeBytes(byte[] json) =>
        Invoke(GetMethod(typeof(ReadOnlyMemory<byte>)), new ReadOnlyMemory<byte>(json));

    private static AIAssetDocumentCodecException FailureString(string json) =>
        Failure(GetMethod(typeof(string)), json);

    private static AIAssetDocumentCodecException FailureBytes(byte[] json) =>
        Failure(GetMethod(typeof(ReadOnlyMemory<byte>)), new ReadOnlyMemory<byte>(json));

    private static MethodInfo GetMethod(Type parameterType)
    {
        var authority = typeof(IAIAssetDocumentCodec).Assembly.GetType(AuthorityType, true)!;
        return authority.GetMethod("Parse", BindingFlags.Static | BindingFlags.NonPublic,
                   binder: null, types: [parameterType], modifiers: null)
               ?? throw new InvalidOperationException("Strict parser method was not found.");
    }

    private static object Invoke(MethodInfo method, object argument) =>
        method.Invoke(null, [argument])
        ?? throw new InvalidOperationException("Strict parser returned null.");

    private static AIAssetDocumentCodecException Failure(MethodInfo method, object argument)
    {
        Action action = () => method.Invoke(null, [argument]);
        return action.Should().Throw<TargetInvocationException>().Which.InnerException.Should()
            .BeOfType<AIAssetDocumentCodecException>().Which;
    }
}
