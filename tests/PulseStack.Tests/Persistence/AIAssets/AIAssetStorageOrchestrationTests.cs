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
    public async Task Writer_Document_ShouldStopAfterValidationFailure()
    {
        var fixture = new Fixture();
        fixture.Validator.Result = AIAssetDocumentValidationResult.Failure(
            new AIAssetDocumentValidationError("AD000", "Invalid.", "$"));

        var act = async () => await fixture.Writer.WriteAsync(fixture.Key, fixture.Document);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.DocumentValidation);
        failure.Which.ValidationResult.Should().BeSameAs(fixture.Validator.Result);
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
        public byte[] WrittenBytes { get; private set; } = [];

        public ValueTask<SerializedAIAssetReadResult> ReadAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            events.Add("read");
            return ValueTask.FromResult(ReadResult);
        }

        public ValueTask<AIAssetWriteResult> WriteAsync(
            AssetDefinitionKey key,
            ReadOnlyMemory<byte> representation,
            CancellationToken cancellationToken = default)
        {
            events.Add("write");
            WrittenBytes = representation.ToArray();
            return ValueTask.FromResult(AIAssetWriteResult.Created);
        }
    }

    private sealed class TestCodec(List<string> events, AIAssetDocument document) : IAIAssetDocumentCodec
    {
        public byte[] CanonicalBytes { get; set; } = [1, 2, 3];

        public byte[] Serialize(AIAssetDocument value)
        {
            events.Add("serialize");
            return CanonicalBytes.ToArray();
        }

        public AIAssetDocument Deserialize(ReadOnlyMemory<byte> utf8Json)
        {
            events.Add("deserialize");
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

        public ValueTask<AIAssetDocumentValidationResult> ValidateAsync(
            AIAssetDocument document,
            CancellationToken cancellationToken = default)
        {
            events.Add("validate");
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class TestMapper(List<string> events) : IAIAssetDocumentMapper
    {
        public IAsset Asset { get; } = new TestAsset();
        public int CallCount { get; private set; }

        public AIAssetDocument ToDocument(IAsset asset) => throw new NotSupportedException();

        public IAsset FromDocument(AIAssetDocument document)
        {
            events.Add("map");
            CallCount++;
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
