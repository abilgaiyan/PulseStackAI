using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
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
            nameof(AIAssetStorageFailureCategory.ProviderFailure));
    }

    [Fact]
    public void StorageOperation_ShouldFreezeOuterOperationVocabulary()
    {
        Enum.GetNames<AIAssetStorageOperation>().Should().Equal(
            nameof(AIAssetStorageOperation.WriteDocument),
            nameof(AIAssetStorageOperation.WriteRepresentation),
            nameof(AIAssetStorageOperation.Load));
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
    public void LoadResult_Loaded_ShouldRejectNullAndPreserveExactAsset()
    {
        var nullAct = () => new AIAssetLoadResult.Loaded(null!);
        var asset = new TestAsset();

        nullAct.Should().Throw<ArgumentNullException>();
        new AIAssetLoadResult.Loaded(asset).Asset.Should().BeSameAs(asset);
    }

    [Fact]
    public void WriterContract_ShouldFreezeBothOverloadsAndCancellationBoundary()
    {
        var methods = typeof(IAIAssetWriter)
            .GetMethods()
            .Where(static method => method.Name == nameof(IAIAssetWriter.WriteAsync))
            .ToArray();

        methods.Should().HaveCount(2);
        methods.Should().ContainSingle(static method => method.GetParameters()[1].ParameterType == typeof(AIAssetDocument));
        methods.Should().ContainSingle(static method => method.GetParameters()[1].ParameterType == typeof(ReadOnlyMemory<byte>));

        foreach (var method in methods)
        {
            method.ReturnType.Should().Be(typeof(ValueTask<AIAssetWriteResult>));
            var parameters = method.GetParameters();
            parameters.Should().HaveCount(3);
            parameters[0].ParameterType.Should().Be<AssetDefinitionKey>();
            parameters[2].ParameterType.Should().Be<CancellationToken>();
            parameters[2].IsOptional.Should().BeTrue();
        }
    }

    [Fact]
    public void LoaderContract_ShouldFreezeSignatureAndCancellationBoundary()
    {
        var method = typeof(IAIAssetLoader).GetMethod(nameof(IAIAssetLoader.LoadAsync));

        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(ValueTask<AIAssetLoadResult>));
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be<AssetDefinitionKey>();
        parameters[1].ParameterType.Should().Be<CancellationToken>();
        parameters[1].IsOptional.Should().BeTrue();
    }

    [Fact]
    public void AssetDefinitionKey_ShouldUseTypeIdAndVersionForExactIdentity()
    {
        var id = AssetId.New();
        var key = new AssetDefinitionKey(AssetType.Agent, id, AssetVersion.Initial);
        var equal = new AssetDefinitionKey(AssetType.Agent, id, AssetVersion.Initial);
        var differentType = new AssetDefinitionKey(AssetType.Workflow, id, AssetVersion.Initial);
        var differentId = new AssetDefinitionKey(AssetType.Agent, AssetId.New(), AssetVersion.Initial);
        var differentVersion = new AssetDefinitionKey(AssetType.Agent, id, new AssetVersion("2.0.0"));

        key.Should().Be(equal);
        key.GetHashCode().Should().Be(equal.GetHashCode());
        key.Should().NotBe(differentType);
        key.Should().NotBe(differentId);
        key.Should().NotBe(differentVersion);

        new HashSet<AssetDefinitionKey> { key, equal, differentType, differentId, differentVersion }
            .Should().HaveCount(4);
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
    public void OperationException_ShouldPreserveCodecClassificationAndOuterContext()
    {
        var key = new AssetDefinitionKey(AssetType.Agent, AssetId.New(), AssetVersion.Initial);
        var context = new AIAssetStorageDiagnosticContext
        {
            Operation = AIAssetStorageOperation.Load,
            Key = key
        };
        var codecFailure = new AIAssetDocumentCodecException(
            AIAssetDocumentCodecOperation.Deserialization,
            AIAssetDocumentCodecFailureReason.InvalidJson,
            "Invalid JSON.");

        var failure = new AIAssetStorageOperationException(
            "AI Asset load failed while decoding the stored representation.",
            context,
            codecFailure);

        failure.Context.Should().BeSameAs(context);
        failure.Context.Operation.Should().Be(AIAssetStorageOperation.Load);
        failure.Context.Key.Should().Be(key);
        failure.InnerException.Should().BeSameAs(codecFailure);
        codecFailure.Operation.Should().Be(AIAssetDocumentCodecOperation.Deserialization);
        codecFailure.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidJson);
        typeof(AIAssetStorageOperationException).GetProperty("Category").Should().BeNull();
    }

    [Fact]
    public void OperationException_ShouldRejectMissingOperationContext()
    {
        var context = new AIAssetStorageDiagnosticContext
        {
            Key = new AssetDefinitionKey(AssetType.Agent, AssetId.New(), AssetVersion.Initial)
        };

        var act = () => new AIAssetStorageOperationException(
            "Operation failed.",
            context,
            new InvalidOperationException());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OperationException_ShouldRejectUndefinedOperationContext()
    {
        var context = new AIAssetStorageDiagnosticContext
        {
            Operation = (AIAssetStorageOperation)999,
            Key = new AssetDefinitionKey(AssetType.Agent, AssetId.New(), AssetVersion.Initial)
        };

        var act = () => new AIAssetStorageOperationException(
            "Operation failed.",
            context,
            new InvalidOperationException());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OperationException_ShouldRejectMissingKeyContext()
    {
        var context = new AIAssetStorageDiagnosticContext
        {
            Operation = AIAssetStorageOperation.Load
        };

        var act = () => new AIAssetStorageOperationException(
            "Operation failed.",
            context,
            new InvalidOperationException());

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OperationException_ShouldRejectInvalidKeyContext()
    {
        var context = new AIAssetStorageDiagnosticContext
        {
            Operation = AIAssetStorageOperation.Load,
            Key = new AssetDefinitionKey(AssetType.Agent, AssetId.Empty, AssetVersion.Initial)
        };

        var act = () => new AIAssetStorageOperationException(
            "Operation failed.",
            context,
            new InvalidOperationException());

        act.Should().Throw<ArgumentException>();
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
    public void DiagnosticContext_ShouldExposeOnlySafeStructuredOperationContext()
    {
        var context = new AIAssetStorageDiagnosticContext
        {
            Operation = AIAssetStorageOperation.WriteRepresentation,
            Key = new AssetDefinitionKey(AssetType.Agent, AssetId.New(), AssetVersion.Initial),
            RepresentationSizeBytes = 128,
            MaximumRepresentationSizeBytes = 1024
        };

        context.Operation.Should().Be(AIAssetStorageOperation.WriteRepresentation);
        context.Key.Should().NotBeNull();

        var propertyTypes = typeof(AIAssetStorageDiagnosticContext)
            .GetProperties()
            .Select(static property => property.PropertyType)
            .ToArray();

        propertyTypes.Should().NotContain(typeof(byte[]));
        propertyTypes.Should().NotContain(typeof(ReadOnlyMemory<byte>));
        propertyTypes.Should().NotContain(typeof(AIAssetDocument));
        propertyTypes.Should().NotContain(typeof(IAsset));
    }

    private sealed class TestAsset : IAsset
    {
        public AssetId Id => throw new NotSupportedException();

        public AssetUrn Urn => throw new NotSupportedException();

        public AssetVersion Version => throw new NotSupportedException();

        public AssetMetadata Metadata => throw new NotSupportedException();

        public AssetType Type => throw new NotSupportedException();

        public AssetLifecycle Lifecycle => throw new NotSupportedException();

        public IReadOnlyCollection<AssetReference> References => throw new NotSupportedException();

        public IReadOnlyCollection<AssetDependency> Dependencies => throw new NotSupportedException();
    }
}
