using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetDocumentCodecContractTests
{
    [Fact]
    public void CodecInterface_ShouldFreezeExactPublicApiSurface()
    {
        var methods = typeof(IAIAssetDocumentCodec)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        methods.Should().HaveCount(6);

        AssertMethod(methods, nameof(IAIAssetDocumentCodec.Serialize), typeof(byte[]), typeof(AIAssetDocument));
        AssertMethod(methods, nameof(IAIAssetDocumentCodec.SerializeToString), typeof(string), typeof(AIAssetDocument));
        AssertMethod(methods, nameof(IAIAssetDocumentCodec.Deserialize), typeof(AIAssetDocument), typeof(ReadOnlyMemory<byte>));
        AssertMethod(methods, nameof(IAIAssetDocumentCodec.Deserialize), typeof(AIAssetDocument), typeof(string));
        AssertMethod(
            methods,
            nameof(IAIAssetDocumentCodec.SerializeAsync),
            typeof(ValueTask),
            typeof(AIAssetDocument),
            typeof(Stream),
            typeof(CancellationToken));
        AssertMethod(
            methods,
            nameof(IAIAssetDocumentCodec.DeserializeAsync),
            typeof(ValueTask<AIAssetDocument>),
            typeof(Stream),
            typeof(CancellationToken));
    }

    [Fact]
    public void StreamMethods_ShouldFreezeOptionalCancellationTokenDefaults()
    {
        var serializeAsync = GetMethod(
            nameof(IAIAssetDocumentCodec.SerializeAsync),
            typeof(AIAssetDocument),
            typeof(Stream),
            typeof(CancellationToken));
        var deserializeAsync = GetMethod(
            nameof(IAIAssetDocumentCodec.DeserializeAsync),
            typeof(Stream),
            typeof(CancellationToken));

        AssertDefaultCancellationToken(serializeAsync.GetParameters()[2]);
        AssertDefaultCancellationToken(deserializeAsync.GetParameters()[1]);
    }

    [Fact]
    public void Operation_ShouldFreezeExactVocabulary()
    {
        Enum.GetNames<AIAssetDocumentCodecOperation>().Should().Equal(
            nameof(AIAssetDocumentCodecOperation.Serialization),
            nameof(AIAssetDocumentCodecOperation.Deserialization));
    }

    [Fact]
    public void FailureReason_ShouldFreezeExactVocabulary()
    {
        Enum.GetNames<AIAssetDocumentCodecFailureReason>().Should().Equal(
            nameof(AIAssetDocumentCodecFailureReason.InvalidEncoding),
            nameof(AIAssetDocumentCodecFailureReason.InvalidJson),
            nameof(AIAssetDocumentCodecFailureReason.UnknownMember),
            nameof(AIAssetDocumentCodecFailureReason.DuplicateMember),
            nameof(AIAssetDocumentCodecFailureReason.MissingRequiredMember),
            nameof(AIAssetDocumentCodecFailureReason.InvalidTokenKind),
            nameof(AIAssetDocumentCodecFailureReason.MalformedSchemaVersion),
            nameof(AIAssetDocumentCodecFailureReason.UnsupportedSchemaVersion),
            nameof(AIAssetDocumentCodecFailureReason.UnknownDiscriminator),
            nameof(AIAssetDocumentCodecFailureReason.UnsupportedDiscriminator),
            nameof(AIAssetDocumentCodecFailureReason.InvalidScalarToken),
            nameof(AIAssetDocumentCodecFailureReason.InvalidScalarValue),
            nameof(AIAssetDocumentCodecFailureReason.UnsupportedDocumentType),
            nameof(AIAssetDocumentCodecFailureReason.DiscriminatorMismatch),
            nameof(AIAssetDocumentCodecFailureReason.InvalidUnicode),
            nameof(AIAssetDocumentCodecFailureReason.UnrepresentableDocument));
    }

    [Fact]
    public void Exception_ShouldPreserveOperationReasonContextAndInnerException()
    {
        var innerException = new InvalidOperationException("inner");

        var exception = new AIAssetDocumentCodecException(
            AIAssetDocumentCodecOperation.Deserialization,
            AIAssetDocumentCodecFailureReason.InvalidScalarToken,
            "message",
            "lifecycle",
            "Draft",
            innerException);

        exception.Operation.Should().Be(AIAssetDocumentCodecOperation.Deserialization);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidScalarToken);
        exception.Message.Should().Be("message");
        exception.MemberName.Should().Be("lifecycle");
        exception.Token.Should().Be("Draft");
        exception.InnerException.Should().BeSameAs(innerException);
    }

    [Fact]
    public void Exception_ShouldPreserveNullOptionalContext()
    {
        var exception = new AIAssetDocumentCodecException(
            AIAssetDocumentCodecOperation.Serialization,
            AIAssetDocumentCodecFailureReason.UnrepresentableDocument,
            "message");

        exception.Operation.Should().Be(AIAssetDocumentCodecOperation.Serialization);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.UnrepresentableDocument);
        exception.MemberName.Should().BeNull();
        exception.Token.Should().BeNull();
        exception.InnerException.Should().BeNull();
    }

    private static void AssertMethod(
        MethodInfo[] methods,
        string name,
        Type returnType,
        params Type[] parameterTypes)
    {
        var method = methods.SingleOrDefault(candidate =>
            candidate.Name == name &&
            candidate.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameterTypes));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(returnType);
    }

    private static MethodInfo GetMethod(string name, params Type[] parameterTypes)
    {
        return typeof(IAIAssetDocumentCodec).GetMethod(name, parameterTypes)
            ?? throw new InvalidOperationException($"Expected method '{name}' was not found.");
    }

    private static void AssertDefaultCancellationToken(ParameterInfo parameter)
    {
        parameter.ParameterType.Should().Be(typeof(CancellationToken));
        parameter.Name.Should().Be("cancellationToken");
        parameter.IsOptional.Should().BeTrue();
        parameter.HasDefaultValue.Should().BeTrue();

        // C# encodes `CancellationToken cancellationToken = default` as a null
        // metadata default for the non-nullable value-type parameter.
        parameter.DefaultValue.Should().BeNull();
    }
}
