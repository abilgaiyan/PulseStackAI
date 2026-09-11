using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Core.Persistence.AIAssets.Storage;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetStorageOrchestrationTests
{
    [Fact]
    public async Task Writer_Document_ShouldValidateThenSerializeThenWriteCanonicalBytes()
    {
        var fixture = new Fixture();

        var result = await fixture.Writer.WriteAsync(fixture.Key, fixture.Document);

        result.Should().Be(AIAssetWriteResult.Created);
        fixture.Events.Should().Equal("validate", "serialize", "write");
        fixture.Store.WrittenBytes.Should().Equal(fixture.Codec.CanonicalBytes);
    }

    [Fact]
    public async Task Writer_Representation_ShouldProveCanonicalBytesBeforeWrite()
    {
        var fixture = new Fixture();
        var bytes = fixture.Codec.CanonicalBytes.AsMemory();

        var result = await fixture.Writer.WriteAsync(fixture.Key, bytes);

        result.Should().Be(AIAssetWriteResult.Created);
        fixture.Events.Should().Equal("deserialize", "validate", "serialize", "write");
        fixture.Store.WrittenBytes.Should().Equal(fixture.Codec.CanonicalBytes);
    }

    [Fact]
    public async Task Writer_Representation_ShouldRejectNonCanonicalBytesWithoutWriting()
    {
        var fixture = new Fixture();
        fixture.Codec.CanonicalBytes = [1, 2, 3];

        var act = async () => await fixture.Writer.WriteAsync(fixture.Key, new byte[] { 1, 2, 3, 4 });

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.NonCanonicalRepresentation);
        fixture.Events.Should().Equal("deserialize", "validate", "serialize");
    }

    [Fact]
    public async Task Writer_Document_ShouldStopAfterValidationFailureAndPreserveOrderedDiagnostics()
    {
        var fixture = new Fixture();
        var first = new AIAssetDocumentValidationError("AD001", "First.", "$.first");
        var second = new AIAssetDocumentValidationError("AD002", "Second.", "$.second");
        fixture.Validator.Result = AIAssetDocumentValidationResult.Failure(first, second);

        var act = async () => await fixture.Writer.WriteAsync(fixture.Key, fixture.Document);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.DocumentValidation);
        failure.Which.ValidationResult.Should().BeSameAs(fixture.Validator.Result);
        failure.Which.ValidationResult!.Errors.Should().Equal(first, second);
        fixture.Events.Should().Equal("validate");
    }

    [Fact]
    public async Task Writer_Document_ShouldStopOnKeyDocumentMismatchBeforeSerialization()
    {
        var fixture = new Fixture();
        var otherKey = new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);

        var act = async () => await fixture.Writer.WriteAsync(otherKey, fixture.Document);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.KeyDocumentMismatch);
        fixture.Events.Should().Equal("validate");
    }

    [Fact]
    public async Task Loader_NotFound_ShouldReturnSuccessfulAbsenceWithoutCodecValidationOrMapping()
    {
        var fixture = new Fixture();
        fixture.Store.ReadResult = new SerializedAIAssetReadResult.NotFound();

        var result = await fixture.Loader.LoadAsync(fixture.Key);

        result.Should().BeOfType<AIAssetLoadResult.NotFound>();
        fixture.Events.Should().Equal("read");
        fixture.Store.ReadCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Loader_ShouldApplySizeGateBeforeDeserialization()
    {
        var fixture = new Fixture(maximumRepresentationSizeBytes: 2);
        fixture.Store.ReadResult = new SerializedAIAssetReadResult.Found(new byte[] { 1, 2, 3 });

        var act = async () => await fixture.Loader.LoadAsync(fixture.Key);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.RepresentationTooLarge);
        fixture.Events.Should().Equal("read");
    }

    [Fact]
    public async Task Loader_ShouldDeserializeValidateProveCanonicalThenMapExactlyOneAsset()
    {
        var fixture = new Fixture();
        fixture.Store.ReadResult = new SerializedAIAssetReadResult.Found(fixture.Codec.CanonicalBytes);

        var result = await fixture.Loader.LoadAsync(fixture.Key);

        var loaded = result.Should().BeOfType<AIAssetLoadResult.Loaded>().Subject;
        loaded.Asset.Should().BeSameAs(fixture.Mapper.Asset);
        fixture.Events.Should().Equal("read", "deserialize", "validate", "serialize", "map");
        fixture.Store.ReadCallCount.Should().Be(1);
        fixture.Mapper.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Loader_ShouldRejectNonCanonicalStoredBytesBeforeMapping()
    {
        var fixture = new Fixture();
        fixture.Codec.CanonicalBytes = [1, 2, 3];
        fixture.Store.ReadResult = new SerializedAIAssetReadResult.Found(new byte[] { 1, 2, 3, 4 });

        var act = async () => await fixture.Loader.LoadAsync(fixture.Key);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.NonCanonicalRepresentation);
        fixture.Events.Should().Equal("read", "deserialize", "validate", "serialize");
        fixture.Mapper.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task InvalidArgument_ShouldWinOverAlreadyCancelledToken()
    {
        var fixture = new Fixture();
        var invalidKey = new AssetDefinitionKey(AssetType.Prompt, AssetId.Empty, AssetVersion.Initial);
        using var source = new CancellationTokenSource();
        source.Cancel();

        var writerAct = async () => await fixture.Writer.WriteAsync(invalidKey, fixture.Document, source.Token);
        await writerAct.Should().ThrowAsync<ArgumentException>();
        fixture.Events.Should().BeEmpty();

        var loaderAct = async () => await fixture.Loader.LoadAsync(invalidKey, source.Token);
        await loaderAct.Should().ThrowAsync<ArgumentException>();
        fixture.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task NullDocument_ShouldWinOverAlreadyCancelledToken()
    {
        var fixture = new Fixture();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var act = async () => await fixture.Writer.WriteAsync(fixture.Key, (AIAssetDocument)null!, source.Token);

        await act.Should().ThrowAsync<ArgumentNullException>();
        fixture.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task EntryCancellation_ShouldWinBeforeAnyCollaboratorWork()
    {
        var fixture = new Fixture();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var writerAct = async () => await fixture.Writer.WriteAsync(fixture.Key, fixture.Document, source.Token);
        await writerAct.Should().ThrowAsync<OperationCanceledException>();
        fixture.Events.Should().BeEmpty();

        var loaderAct = async () => await fixture.Loader.LoadAsync(fixture.Key, source.Token);
        await loaderAct.Should().ThrowAsync<OperationCanceledException>();
        fixture.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task AsyncCollaborators_ShouldReceiveExactCallerToken()
    {
        var fixture = new Fixture();
        using var source = new CancellationTokenSource();

        await fixture.Writer.WriteAsync(fixture.Key, fixture.Document, source.Token);
        fixture.Validator.LastToken.Should().Be(source.Token);
        fixture.Store.LastWriteToken.Should().Be(source.Token);

        fixture.Store.ReadResult = new SerializedAIAssetReadResult.Found(fixture.Codec.CanonicalBytes);
        await fixture.Loader.LoadAsync(fixture.Key, source.Token);
        fixture.Store.LastReadToken.Should().Be(source.Token);
        fixture.Validator.LastToken.Should().Be(source.Token);
    }

    [Fact]
    public async Task Writer_ShouldObserveCancellationIntroducedBySuccessfulValidationBeforeKeyAgreement()
    {
        var fixture = new Fixture();
        using var source = new CancellationTokenSource();
        fixture.Validator.CancelSourceOnReturn = source;

        var act = async () => await fixture.Writer.WriteAsync(fixture.Key, fixture.Document, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        fixture.Events.Should().Equal("validate");
    }

    [Fact]
    public async Task Loader_ShouldObserveCancellationIntroducedByReadBeforeInterpretingNotFound()
    {
        var fixture = new Fixture();
        using var source = new CancellationTokenSource();
        fixture.Store.ReadResult = new SerializedAIAssetReadResult.NotFound();
        fixture.Store.CancelSourceOnReadReturn = source;

        var act = async () => await fixture.Loader.LoadAsync(fixture.Key, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        fixture.Events.Should().Equal("read");
    }

    [Fact]
    public async Task Loader_ShouldObserveCancellationIntroducedBySuccessfulValidationBeforeKeyAgreement()
    {
        var fixture = new Fixture();
        using var source = new CancellationTokenSource();
        fixture.Store.ReadResult = new SerializedAIAssetReadResult.Found(fixture.Codec.CanonicalBytes);
        fixture.Validator.CancelSourceOnReturn = source;

        var act = async () => await fixture.Loader.LoadAsync(fixture.Key, source.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        fixture.Events.Should().Equal("read", "deserialize", "validate");
        fixture.Mapper.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Writer_CodecFailure_ShouldPreserveCodecClassificationAndWriteOperationContext()
    {
        var fixture = new Fixture();
        var codecFailure = new AIAssetDocumentCodecException(
            AIAssetDocumentCodecOperation.Deserialization,
            AIAssetDocumentCodecFailureReason.InvalidJson,
            "Invalid JSON.");
        fixture.Codec.DeserializeException = codecFailure;

        var act = async () => await fixture.Writer.WriteAsync(fixture.Key, fixture.Codec.CanonicalBytes.AsMemory());

        var failure = await act.Should().ThrowAsync<AIAssetStorageOperationException>();
        failure.Which.InnerException.Should().BeSameAs(codecFailure);
        failure.Which.Context.Operation.Should().Be(AIAssetStorageOperation.WriteRepresentation);
        failure.Which.Context.Key.Should().Be(fixture.Key);
        fixture.Events.Should().Equal("deserialize");
    }

    [Fact]
    public async Task Loader_CodecFailure_ShouldPreserveCodecClassificationAndLoadContext()
    {
        var fixture = new Fixture();
        fixture.Store.ReadResult = new SerializedAIAssetReadResult.Found(fixture.Codec.CanonicalBytes);
        var codecFailure = new AIAssetDocumentCodecException(
            AIAssetDocumentCodecOperation.Deserialization,
            AIAssetDocumentCodecFailureReason.InvalidJson,
            "Invalid JSON.");
        fixture.Codec.DeserializeException = codecFailure;

        var act = async () => await fixture.Loader.LoadAsync(fixture.Key);

        var failure = await act.Should().ThrowAsync<AIAssetStorageOperationException>();
        failure.Which.InnerException.Should().BeSameAs(codecFailure);
        failure.Which.Context.Operation.Should().Be(AIAssetStorageOperation.Load);
        failure.Which.Context.Key.Should().Be(fixture.Key);
        fixture.Events.Should().Equal("read", "deserialize");
    }

    [Fact]
    public async Task Loader_ShouldStopAfterValidationFailureAndPreserveOrderedDiagnostics()
    {
        var fixture = new Fixture();
        fixture.Store.ReadResult = new SerializedAIAssetReadResult.Found(fixture.Codec.CanonicalBytes);
        var first = new AIAssetDocumentValidationError("AD001", "First.", "$.first");
        var second = new AIAssetDocumentValidationError("AD002", "Second.", "$.second");
        fixture.Validator.Result = AIAssetDocumentValidationResult.Failure(first, second);

        var act = async () => await fixture.Loader.LoadAsync(fixture.Key);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.DocumentValidation);
        failure.Which.ValidationResult.Should().BeSameAs(fixture.Validator.Result);
        failure.Which.ValidationResult!.Errors.Should().Equal(first, second);
        fixture.Events.Should().Equal("read", "deserialize", "validate");
        fixture.Mapper.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task Loader_ShouldStopOnKeyMismatchBeforeCanonicalProofOrMapping()
    {
        var fixture = new Fixture();
        fixture.Store.ReadResult = new SerializedAIAssetReadResult.Found(fixture.Codec.CanonicalBytes);
        var otherKey = new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);

        var act = async () => await fixture.Loader.LoadAsync(otherKey);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.KeyDocumentMismatch);
        fixture.Events.Should().Equal("read", "deserialize", "validate");
        fixture.Mapper.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(AIAssetWriteResult.Created)]
    [InlineData(AIAssetWriteResult.AlreadyPresent)]
    [InlineData(AIAssetWriteResult.Conflict)]
    public async Task Writer_ShouldPropagateTerminalRawWriteResult(AIAssetWriteResult expected)
    {
        var fixture = new Fixture();
        fixture.Store.WriteResult = expected;

        var result = await fixture.Writer.WriteAsync(fixture.Key, fixture.Document);

        result.Should().Be(expected);
        fixture.Events.Should().Equal("validate", "serialize", "write");
    }

    [Fact]
    public async Task RawStoreOrdinaryFailures_ShouldSurfaceAsProviderFailure()
    {
        var writerFixture = new Fixture();
        var writeFailure = new InvalidOperationException("write failed");
        writerFixture.Store.WriteException = writeFailure;

        var writerAct = async () => await writerFixture.Writer.WriteAsync(writerFixture.Key, writerFixture.Document);
        var writerResult = await writerAct.Should().ThrowAsync<AIAssetStorageException>();
        writerResult.Which.Category.Should().Be(AIAssetStorageFailureCategory.ProviderFailure);
        writerResult.Which.InnerException.Should().BeSameAs(writeFailure);
        writerResult.Which.Context!.Operation.Should().Be(AIAssetStorageOperation.WriteDocument);

        var loaderFixture = new Fixture();
        var readFailure = new InvalidOperationException("read failed");
        loaderFixture.Store.ReadException = readFailure;

        var loaderAct = async () => await loaderFixture.Loader.LoadAsync(loaderFixture.Key);
        var loaderResult = await loaderAct.Should().ThrowAsync<AIAssetStorageException>();
        loaderResult.Which.Category.Should().Be(AIAssetStorageFailureCategory.ProviderFailure);
        loaderResult.Which.InnerException.Should().BeSameAs(readFailure);
        loaderResult.Which.Context!.Operation.Should().Be(AIAssetStorageOperation.Load);
    }

    [Fact]
    public async Task RawStoreStorageFailuresWithNonProviderCategory_ShouldBeReclassifiedAsProviderFailure()
    {
        var writerFixture = new Fixture();
        var writeFailure = new AIAssetStorageException(
            AIAssetStorageFailureCategory.NonCanonicalRepresentation,
            "provider misclassified failure");
        writerFixture.Store.WriteException = writeFailure;

        var writerAct = async () => await writerFixture.Writer.WriteAsync(writerFixture.Key, writerFixture.Document);
        var writerResult = await writerAct.Should().ThrowAsync<AIAssetStorageException>();
        writerResult.Which.Category.Should().Be(AIAssetStorageFailureCategory.ProviderFailure);
        writerResult.Which.InnerException.Should().BeSameAs(writeFailure);

        var loaderFixture = new Fixture();
        var readFailure = new AIAssetStorageException(
            AIAssetStorageFailureCategory.Mapping,
            "provider misclassified failure");
        loaderFixture.Store.ReadException = readFailure;

        var loaderAct = async () => await loaderFixture.Loader.LoadAsync(loaderFixture.Key);
        var loaderResult = await loaderAct.Should().ThrowAsync<AIAssetStorageException>();
        loaderResult.Which.Category.Should().Be(AIAssetStorageFailureCategory.ProviderFailure);
        loaderResult.Which.InnerException.Should().BeSameAs(readFailure);
    }

    [Fact]
    public async Task RawStoreCallerCancellation_ShouldPropagateAsCancellation()
    {
        var writerFixture = new Fixture();
        using var writeSource = new CancellationTokenSource();
        writerFixture.Store.WriteAction = token =>
        {
            writeSource.Cancel();
            throw new OperationCanceledException(token);
        };

        var writerAct = async () => await writerFixture.Writer.WriteAsync(writerFixture.Key, writerFixture.Document, writeSource.Token);
        await writerAct.Should().ThrowAsync<OperationCanceledException>();

        var loaderFixture = new Fixture();
        using var readSource = new CancellationTokenSource();
        loaderFixture.Store.ReadAction = token =>
        {
            readSource.Cancel();
            throw new OperationCanceledException(token);
        };

        var loaderAct = async () => await loaderFixture.Loader.LoadAsync(loaderFixture.Key, readSource.Token);
        await loaderAct.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RawStoreNonCallerCancellation_ShouldSurfaceAsProviderFailure()
    {
        var writerFixture = new Fixture();
        var writeFailure = new OperationCanceledException();
        writerFixture.Store.WriteException = writeFailure;

        var writerAct = async () => await writerFixture.Writer.WriteAsync(writerFixture.Key, writerFixture.Document);
        var writerResult = await writerAct.Should().ThrowAsync<AIAssetStorageException>();
        writerResult.Which.Category.Should().Be(AIAssetStorageFailureCategory.ProviderFailure);
        writerResult.Which.InnerException.Should().BeSameAs(writeFailure);

        var loaderFixture = new Fixture();
        var readFailure = new OperationCanceledException();
        loaderFixture.Store.ReadException = readFailure;

        var loaderAct = async () => await loaderFixture.Loader.LoadAsync(loaderFixture.Key);
        var loaderResult = await loaderAct.Should().ThrowAsync<AIAssetStorageException>();
        loaderResult.Which.Category.Should().Be(AIAssetStorageFailureCategory.ProviderFailure);
        loaderResult.Which.InnerException.Should().BeSameAs(readFailure);
    }

    [Fact]
    public async Task Loader_MapperFailure_ShouldSurfaceAsMappingFailure()
    {
        var fixture = new Fixture();
        fixture.Store.ReadResult = new SerializedAIAssetReadResult.Found(fixture.Codec.CanonicalBytes);
        var mappingFailure = new InvalidOperationException("mapping failed");
        fixture.Mapper.Exception = mappingFailure;

        var act = async () => await fixture.Loader.LoadAsync(fixture.Key);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.Mapping);
        failure.Which.InnerException.Should().BeSameAs(mappingFailure);
        failure.Which.Context!.Operation.Should().Be(AIAssetStorageOperation.Load);
        fixture.Events.Should().Equal("read", "deserialize", "validate", "serialize", "map");
    }

    private sealed class Fixture
    {
        public Fixture(long maximumRepresentationSizeBytes = 1024)
        {
            Key = new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);
            Document = CreateDocument(Key);
            Events = [];
            Store = new TestStore(Events);
            Codec = new TestCodec(Events, Document);
            Validator = new TestValidator(Events);
            Mapper = new TestMapper(Events);
            var options = new AIAssetStorageOptions
            {
                MaximumRepresentationSizeBytes = maximumRepresentationSizeBytes
            };

            Writer = new AIAssetWriter(Store, Codec, Validator, options);
            Loader = new AIAssetLoader(Store, Codec, Validator, Mapper, options);
        }

        public AssetDefinitionKey Key { get; }
        public PromptAssetDocument Document { get; }
        public List<string> Events { get; }
        public TestStore Store { get; }
        public TestCodec Codec { get; }
        public TestValidator Validator { get; }
        public TestMapper Mapper { get; }
        public AIAssetWriter Writer { get; }
        public AIAssetLoader Loader { get; }
    }

    private static PromptAssetDocument CreateDocument(AssetDefinitionKey key) => new(
        AIAssetSchemaVersion.V1,
        new AIAssetIdentityDocument
        {
            Id = key.Id.Value.ToString(),
            Urn = $"urn:pulsestack:prompt:{key.Id.Value}",
            Version = key.Version.Value
        },
        new AIAssetMetadataDocument("Prompt"),
        AIAssetLifecycleDocument.Published,
        "System instructions.");

    private sealed class TestStore(List<string> events) : ISerializedAIAssetStore
    {
        public SerializedAIAssetReadResult ReadResult { get; set; } = new SerializedAIAssetReadResult.NotFound();
        public AIAssetWriteResult WriteResult { get; set; } = AIAssetWriteResult.Created;
        public Exception? ReadException { get; set; }
        public Exception? WriteException { get; set; }
        public Action<CancellationToken>? ReadAction { get; set; }
        public Action<CancellationToken>? WriteAction { get; set; }
        public CancellationTokenSource? CancelSourceOnReadReturn { get; set; }
        public CancellationToken LastReadToken { get; private set; }
        public CancellationToken LastWriteToken { get; private set; }
        public int ReadCallCount { get; private set; }
        public byte[] WrittenBytes { get; private set; } = [];

        public ValueTask<SerializedAIAssetReadResult> ReadAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            events.Add("read");
            ReadCallCount++;
            LastReadToken = cancellationToken;
            ReadAction?.Invoke(cancellationToken);
            if (ReadException is not null)
            {
                throw ReadException;
            }

            CancelSourceOnReadReturn?.Cancel();
            return ValueTask.FromResult(ReadResult);
        }

        public ValueTask<AIAssetWriteResult> WriteAsync(
            AssetDefinitionKey key,
            ReadOnlyMemory<byte> representation,
            CancellationToken cancellationToken = default)
        {
            events.Add("write");
            LastWriteToken = cancellationToken;
            WriteAction?.Invoke(cancellationToken);
            if (WriteException is not null)
            {
                throw WriteException;
            }

            WrittenBytes = representation.ToArray();
            return ValueTask.FromResult(WriteResult);
        }
    }

    private sealed class TestCodec(List<string> events, AIAssetDocument document) : IAIAssetDocumentCodec
    {
        public byte[] CanonicalBytes { get; set; } = [1, 2, 3];
        public AIAssetDocumentCodecException? SerializeException { get; set; }
        public AIAssetDocumentCodecException? DeserializeException { get; set; }

        public byte[] Serialize(AIAssetDocument value)
        {
            events.Add("serialize");
            if (SerializeException is not null)
            {
                throw SerializeException;
            }

            return CanonicalBytes.ToArray();
        }

        public AIAssetDocument Deserialize(ReadOnlyMemory<byte> utf8Json)
        {
            events.Add("deserialize");
            if (DeserializeException is not null)
            {
                throw DeserializeException;
            }

            return document;
        }

        public string SerializeToString(AIAssetDocument value) => throw new NotSupportedException();
        public AIAssetDocument Deserialize(string json) => throw new NotSupportedException();
        public ValueTask SerializeAsync(AIAssetDocument value, Stream output, CancellationToken cancellationToken = default)
            => ValueTask.FromException(new NotSupportedException());
        public ValueTask<AIAssetDocument> DeserializeAsync(Stream input, CancellationToken cancellationToken = default)
            => ValueTask.FromException<AIAssetDocument>(new NotSupportedException());
    }

    private sealed class TestValidator(List<string> events) : IAIAssetDocumentValidator
    {
        public AIAssetDocumentValidationResult Result { get; set; } = AIAssetDocumentValidationResult.Success();
        public CancellationTokenSource? CancelSourceOnReturn { get; set; }
        public CancellationToken LastToken { get; private set; }

        public ValueTask<AIAssetDocumentValidationResult> ValidateAsync(
            AIAssetDocument document,
            CancellationToken cancellationToken = default)
        {
            events.Add("validate");
            LastToken = cancellationToken;
            CancelSourceOnReturn?.Cancel();
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class TestMapper(List<string> events) : IAIAssetDocumentMapper
    {
        public IAsset Asset { get; } = new TestAsset();
        public Exception? Exception { get; set; }
        public int CallCount { get; private set; }

        public AIAssetDocument ToDocument(IAsset asset) => throw new NotSupportedException();

        public IAsset FromDocument(AIAssetDocument document)
        {
            events.Add("map");
            CallCount++;
            if (Exception is not null)
            {
                throw Exception;
            }

            return Asset;
        }
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
