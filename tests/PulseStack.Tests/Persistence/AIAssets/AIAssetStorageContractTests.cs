using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetStorageContractTests
{
    [Fact]
    public void WriteResult_ShouldFreezeSemanticOutcomeVocabulary()
    {
        Enum.GetNames<AIAssetWriteResult>().Should().Equal(
            nameof(AIAssetWriteResult.Created),
            nameof(AIAssetWriteResult.AlreadyPresent),
            nameof(AIAssetWriteResult.Conflict));
    }

    [Fact]
    public void FailureCategory_ShouldKeepPipelineFailuresProgrammaticallyDistinct()
    {
        Enum.GetNames<AIAssetStorageFailureCategory>().Should().Equal(
            nameof(AIAssetStorageFailureCategory.RepresentationTooLarge),
            nameof(AIAssetStorageFailureCategory.DocumentValidation),
            nameof(AIAssetStorageFailureCategory.KeyDocumentMismatch),
            nameof(AIAssetStorageFailureCategory.NonCanonicalRepresentation),
            nameof(AIAssetStorageFailureCategory.CompositionConfiguration),
            nameof(AIAssetStorageFailureCategory.Mapping),
            nameof(AIAssetStorageFailureCategory.ProviderStorage));
    }

    [Fact]
    public void ReadResult_ShouldRepresentNotFoundWithoutNull()
    {
        SerializedAIAssetReadResult result = new SerializedAIAssetReadResult.NotFound();

        result.Should().BeOfType<SerializedAIAssetReadResult.NotFound>();
    }

    [Fact]
    public void ReadResult_Found_ShouldCarryReadOnlyMemory()
    {
        ReadOnlyMemory<byte> bytes = new byte[] { 1, 2, 3 };

        var result = new SerializedAIAssetReadResult.Found(bytes);

        result.Representation.ToArray().Should().Equal(1, 2, 3);
    }

    [Fact]
    public void LoadResult_ShouldRepresentNotFoundWithoutNull()
    {
        AIAssetLoadResult result = new AIAssetLoadResult.NotFound();

        result.Should().BeOfType<AIAssetLoadResult.NotFound>();
    }

    [Fact]
    public void Contract_ShouldAcceptAllSupportedSchemaV1RootTypes()
    {
        var supported = new[]
        {
            AssetType.Project,
            AssetType.Library,
            AssetType.Package,
            AssetType.Workflow,
            AssetType.Agent,
            AssetType.Prompt,
            AssetType.Tool,
            AssetType.Knowledge,
            AssetType.Memory,
            AssetType.Policy,
            AssetType.Model
        };

        foreach (var type in supported)
        {
            var key = new AssetDefinitionKey(type, AssetId.New(), AssetVersion.Initial);
            var act = () => AIAssetStorageContract.EnsureValidKey(key);
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void Contract_ShouldRejectReservedProviderTypeAsArgumentFailure()
    {
        var key = new AssetDefinitionKey(AssetType.Provider, AssetId.New(), AssetVersion.Initial);

        var act = () => AIAssetStorageContract.EnsureValidKey(key);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Contract_ShouldRejectEmptyAssetIdAsArgumentFailure()
    {
        var key = new AssetDefinitionKey(AssetType.Agent, AssetId.Empty, AssetVersion.Initial);

        var act = () => AIAssetStorageContract.EnsureValidKey(key);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Contract_ShouldRejectMissingVersionAsArgumentFailure()
    {
        var key = new AssetDefinitionKey(AssetType.Agent, AssetId.New(), null!);

        var act = () => AIAssetStorageContract.EnsureValidKey(key);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void InvalidStaticSizePolicy_ShouldBeCompositionConfigurationFailure()
    {
        var options = new AIAssetStorageOptions { MaximumRepresentationSizeBytes = 0 };

        var act = () => AIAssetStorageContract.EnsureValidOptions(options);

        act.Should().Throw<AIAssetStorageException>()
            .Which.Category.Should().Be(AIAssetStorageFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void MissingStaticOptions_ShouldBeCompositionConfigurationFailure()
    {
        var act = () => AIAssetStorageContract.EnsureValidOptions(null);

        act.Should().Throw<AIAssetStorageException>()
            .Which.Category.Should().Be(AIAssetStorageFailureCategory.CompositionConfiguration);
    }

    [Fact]
    public void ValidationFailure_ShouldPreserveCompleteValidationResult()
    {
        var validation = AIAssetDocumentValidationResult.Failure(
            new AIAssetDocumentValidationError("AD000", "Invalid document.", "$"));
        var failure = new AIAssetStorageException(
            AIAssetStorageFailureCategory.DocumentValidation,
            "Document validation failed.",
            validationResult: validation);

        failure.ValidationResult.Should().BeSameAs(validation);
        failure.ValidationResult!.Errors.Should().ContainSingle();
        failure.Category.Should().Be(AIAssetStorageFailureCategory.DocumentValidation);
    }

    [Fact]
    public void PortableContracts_ShouldNotExposeForbiddenStorageOperationsOrStreams()
    {
        var contractTypes = new[]
        {
            typeof(ISerializedAIAssetStore),
            typeof(IAIAssetWriter),
            typeof(IAIAssetLoader)
        };

        var methods = contractTypes.SelectMany(static type => type.GetMethods()).ToArray();
        var methodNames = methods.Select(static method => method.Name).ToArray();

        methodNames.Should().NotContain("DeleteAsync");
        methodNames.Should().NotContain("ExistsAsync");
        methodNames.Should().NotContain("UpdateAsync");
        methodNames.Should().NotContain("ReplaceAsync");
        methodNames.Should().NotContain("UpsertAsync");

        methods.SelectMany(static method => method.GetParameters())
            .Select(static parameter => parameter.ParameterType)
            .Should().NotContain(typeof(Stream));
    }

    [Fact]
    public void RawStore_ShouldUseExactDefinitionKeyAndReadOnlyMemoryCarrier()
    {
        var write = typeof(ISerializedAIAssetStore).GetMethod(nameof(ISerializedAIAssetStore.WriteAsync));
        var read = typeof(ISerializedAIAssetStore).GetMethod(nameof(ISerializedAIAssetStore.ReadAsync));

        write.Should().NotBeNull();
        read.Should().NotBeNull();
        write!.GetParameters()[0].ParameterType.Should().Be<AssetDefinitionKey>();
        write.GetParameters()[1].ParameterType.Should().Be<ReadOnlyMemory<byte>>();
        read!.GetParameters()[0].ParameterType.Should().Be<AssetDefinitionKey>();
    }

    [Fact]
    public void DiagnosticContext_ShouldNotExposeSerializedRepresentationOrDocumentContent()
    {
        var propertyTypes = typeof(AIAssetStorageDiagnosticContext)
            .GetProperties()
            .Select(static property => property.PropertyType)
            .ToArray();

        propertyTypes.Should().NotContain(typeof(byte[]));
        propertyTypes.Should().NotContain(typeof(ReadOnlyMemory<byte>));
        propertyTypes.Should().NotContain(typeof(AIAssetDocument));
        propertyTypes.Should().NotContain(typeof(IAsset));
    }
}
