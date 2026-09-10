using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetDocumentRootDiscriminatorTests
{
    private const string AuthorityTypeName =
        "PulseStack.Abstractions.Persistence.AIAssets.Serialization.AIAssetDocumentRootDiscriminator";

    public static TheoryData<string, AIAssetDocumentType, Type> SupportedRoots => new()
    {
        { "project", AIAssetDocumentType.Project, typeof(ProjectAssetDocument) },
        { "library", AIAssetDocumentType.Library, typeof(LibraryAssetDocument) },
        { "package", AIAssetDocumentType.Package, typeof(PackageAssetDocument) },
        { "workflow", AIAssetDocumentType.Workflow, typeof(WorkflowAssetDocument) },
        { "agent", AIAssetDocumentType.Agent, typeof(AgentAssetDocument) },
        { "prompt", AIAssetDocumentType.Prompt, typeof(PromptAssetDocument) },
        { "tool", AIAssetDocumentType.Tool, typeof(ToolAssetDocument) },
        { "knowledge", AIAssetDocumentType.Knowledge, typeof(KnowledgeAssetDocument) },
        { "memory", AIAssetDocumentType.Memory, typeof(MemoryAssetDocument) },
        { "policy", AIAssetDocumentType.Policy, typeof(PolicyAssetDocument) },
        { "model", AIAssetDocumentType.Model, typeof(ModelAssetDocument) }
    };

    [Theory]
    [MemberData(nameof(SupportedRoots))]
    public void Deserialization_ShouldResolveExactSchemaV1RootVocabulary(
        string token,
        AIAssetDocumentType assetType,
        Type documentType)
    {
        var descriptor = Invoke(
            GetMethod("ResolveForDeserialization", typeof(string)),
            token);

        ReadDescriptor(descriptor).Should().Be((token, assetType, documentType));
    }

    [Theory]
    [MemberData(nameof(SupportedRoots))]
    public void Serialization_ShouldResolveExactConcreteRootDocumentVocabulary(
        string token,
        AIAssetDocumentType assetType,
        Type documentType)
    {
        var descriptor = Invoke(
            GetMethod(
                "ResolveForSerialization",
                typeof(Type),
                typeof(AIAssetDocumentType)),
            documentType,
            assetType);

        ReadDescriptor(descriptor).Should().Be((token, assetType, documentType));
    }

    [Fact]
    public void Deserialization_ShouldRejectReservedProviderRoot()
    {
        var exception = InvokeCodecFailure(
            GetMethod("ResolveForDeserialization", typeof(string)),
            "provider");

        exception.Operation.Should().Be(AIAssetDocumentCodecOperation.Deserialization);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.UnsupportedDiscriminator);
        exception.MemberName.Should().Be("assetType");
        exception.Token.Should().Be("provider");
    }

    [Theory]
    [InlineData("Project")]
    [InlineData("PROVIDER")]
    [InlineData("future")]
    [InlineData("")]
    public void Deserialization_ShouldRejectUnknownOrCaseVariantRootTokens(string token)
    {
        var exception = InvokeCodecFailure(
            GetMethod("ResolveForDeserialization", typeof(string)),
            token);

        exception.Operation.Should().Be(AIAssetDocumentCodecOperation.Deserialization);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.UnknownDiscriminator);
        exception.MemberName.Should().Be("assetType");
        exception.Token.Should().Be(token);
    }

    [Fact]
    public void Serialization_ShouldRejectConcreteTypeAndAssetTypeMismatch()
    {
        var exception = InvokeCodecFailure(
            GetMethod(
                "ResolveForSerialization",
                typeof(Type),
                typeof(AIAssetDocumentType)),
            typeof(ProjectAssetDocument),
            AIAssetDocumentType.Library);

        exception.Operation.Should().Be(AIAssetDocumentCodecOperation.Serialization);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.DiscriminatorMismatch);
        exception.MemberName.Should().Be("assetType");
    }

    [Fact]
    public void Serialization_ShouldRejectUnsupportedRootDocumentType()
    {
        var exception = InvokeCodecFailure(
            GetMethod(
                "ResolveForSerialization",
                typeof(Type),
                typeof(AIAssetDocumentType)),
            typeof(AIAssetDocument),
            AIAssetDocumentType.Project);

        exception.Operation.Should().Be(AIAssetDocumentCodecOperation.Serialization);
        exception.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.UnsupportedDocumentType);
        exception.MemberName.Should().BeNull();
        exception.Token.Should().BeNull();
    }

    private static MethodInfo GetMethod(string name, params Type[] parameterTypes)
    {
        var authority = typeof(IAIAssetDocumentCodec).Assembly.GetType(
            AuthorityTypeName,
            throwOnError: true)!;

        return authority.GetMethod(
                   name,
                   BindingFlags.Static | BindingFlags.NonPublic,
                   binder: null,
                   types: parameterTypes,
                   modifiers: null)
               ?? throw new InvalidOperationException($"Method '{name}' was not found.");
    }

    private static object Invoke(MethodInfo method, params object?[] arguments)
    {
        return method.Invoke(null, arguments)
               ?? throw new InvalidOperationException("Root discriminator returned null.");
    }

    private static AIAssetDocumentCodecException InvokeCodecFailure(
        MethodInfo method,
        params object?[] arguments)
    {
        var action = () => method.Invoke(null, arguments);

        var invocationException = action.Should().Throw<TargetInvocationException>().Which;
        return invocationException.InnerException.Should()
            .BeOfType<AIAssetDocumentCodecException>()
            .Which;
    }

    private static (string Token, AIAssetDocumentType AssetType, Type DocumentType) ReadDescriptor(
        object descriptor)
    {
        var descriptorType = descriptor.GetType();

        return (
            (string)descriptorType.GetProperty("Token")!.GetValue(descriptor)!,
            (AIAssetDocumentType)descriptorType.GetProperty("AssetType")!.GetValue(descriptor)!,
            (Type)descriptorType.GetProperty("DocumentType")!.GetValue(descriptor)!);
    }
}
