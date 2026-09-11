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

public sealed class AIAssetStorageCancellationCausalityTests
{
    [Fact]
    public async Task Writer_ProviderOceWithDifferentToken_ShouldBeProviderFailureEvenWhenCallerTokenIsCancelled()
    {
        var fixture = new Fixture();
        using var callerSource = new CancellationTokenSource();
        using var providerSource = new CancellationTokenSource();
        providerSource.Cancel();
        var providerCancellation = new OperationCanceledException(providerSource.Token);
        fixture.Store.Write = (_, _, _) =>
        {
            callerSource.Cancel();
            throw providerCancellation;
        };

        var act = async () => await fixture.Writer.WriteAsync(
            fixture.Key,
            fixture.Document,
            callerSource.Token);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.ProviderFailure);
        failure.Which.InnerException.Should().BeSameAs(providerCancellation);
        failure.Which.Context!.Operation.Should().Be(AIAssetStorageOperation.WriteDocument);
        failure.Which.Context.Key.Should().Be(fixture.Key);
    }

    [Fact]
    public async Task Loader_ProviderOceWithDifferentToken_ShouldBeProviderFailureEvenWhenCallerTokenIsCancelled()
    {
        var fixture = new Fixture();
        using var callerSource = new CancellationTokenSource();
        using var providerSource = new CancellationTokenSource();
        providerSource.Cancel();
        var providerCancellation = new OperationCanceledException(providerSource.Token);
        fixture.Store.Read = (_, _) =>
        {
            callerSource.Cancel();
            throw providerCancellation;
        };

        var act = async () => await fixture.Loader.LoadAsync(fixture.Key, callerSource.Token);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.ProviderFailure);
        failure.Which.InnerException.Should().BeSameAs(providerCancellation);
        failure.Which.Context!.Operation.Should().Be(AIAssetStorageOperation.Load);
        failure.Which.Context.Key.Should().Be(fixture.Key);
    }

    [Fact]
    public async Task Loader_MapperUnrelatedOce_ShouldRemainMappingFailureWhenMapperCancelsCallerToken()
    {
        var fixture = new Fixture();
        using var callerSource = new CancellationTokenSource();
        using var mapperSource = new CancellationTokenSource();
        mapperSource.Cancel();
        var mapperCancellation = new OperationCanceledException(mapperSource.Token);
        fixture.Store.Read = (_, _) => ValueTask.FromResult<SerializedAIAssetReadResult>(
            new SerializedAIAssetReadResult.Found(fixture.Codec.CanonicalBytes));
        fixture.Mapper.Map = _ =>
        {
            callerSource.Cancel();
            throw mapperCancellation;
        };

        var act = async () => await fixture.Loader.LoadAsync(fixture.Key, callerSource.Token);

        var failure = await act.Should().ThrowAsync<AIAssetStorageException>();
        failure.Which.Category.Should().Be(AIAssetStorageFailureCategory.Mapping);
        failure.Which.InnerException.Should().BeSameAs(mapperCancellation);
        failure.Which.Context!.Operation.Should().Be(AIAssetStorageOperation.Load);
        callerSource.IsCancellationRequested.Should().BeTrue();
    }

    [Theory]
    [InlineData(AIAssetWriteResult.Created)]
    [InlineData(AIAssetWriteResult.AlreadyPresent)]
    [InlineData(AIAssetWriteResult.Conflict)]
    public async Task Writer_TerminalStoreResult_ShouldRemainAuthoritativeWhenStoreCancelsCallerToken(
        AIAssetWriteResult expected)
    {
        var fixture = new Fixture();
        using var callerSource = new CancellationTokenSource();
        fixture.Store.Write = (_, _, _) =>
        {
            callerSource.Cancel();
            return ValueTask.FromResult(expected);
        };

        var result = await fixture.Writer.WriteAsync(
            fixture.Key,
            fixture.Document,
            callerSource.Token);

        result.Should().Be(expected);
        callerSource.IsCancellationRequested.Should().BeTrue();
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Key = new AssetDefinitionKey(AssetType.Prompt, AssetId.New(), AssetVersion.Initial);
            Document = new PromptAssetDocument(
                AIAssetSchemaVersion.V1,
                new AIAssetIdentityDocument
                {
                    Id = Key.Id.Value.ToString(),
                    Urn = $"urn:pulsestack:prompt:{Key.Id.Value}",
                    Version = Key.Version.Value
                },
                new AIAssetMetadataDocument("Prompt"),
                AIAssetLifecycleDocument.Published,
                "System instructions.");

            Store = new TestStore();
            Codec = new TestCodec(Document);
            Validator = new TestValidator();
            Mapper = new TestMapper();
            var options = new AIAssetStorageOptions { MaximumRepresentationSizeBytes = 1024 };
            Writer = new AIAssetWriter(Store, Codec, Validator, options);
            Loader = new AIAssetLoader(Store, Codec, Validator, Mapper, options);
        }

        public AssetDefinitionKey Key { get; }
        public PromptAssetDocument Document { get; }
        public TestStore Store { get; }
        public TestCodec Codec { get; }
        public TestValidator Validator { get; }
        public TestMapper Mapper { get; }
        public AIAssetWriter Writer { get; }
        public AIAssetLoader Loader { get; }
    }

    private sealed class TestStore : ISerializedAIAssetStore
    {
        public Func<AssetDefinitionKey, CancellationToken, ValueTask<SerializedAIAssetReadResult>>? Read { get; set; }

        public Func<AssetDefinitionKey, ReadOnlyMemory<byte>, CancellationToken, ValueTask<AIAssetWriteResult>>? Write { get; set; }

        public ValueTask<SerializedAIAssetReadResult> ReadAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
            => Read?.Invoke(key, cancellationToken)
                ?? ValueTask.FromResult<SerializedAIAssetReadResult>(new SerializedAIAssetReadResult.NotFound());

        public ValueTask<AIAssetWriteResult> WriteAsync(
            AssetDefinitionKey key,
            ReadOnlyMemory<byte> representation,
            CancellationToken cancellationToken = default)
            => Write?.Invoke(key, representation, cancellationToken)
                ?? ValueTask.FromResult(AIAssetWriteResult.Created);
    }

    private sealed class TestCodec(AIAssetDocument document) : IAIAssetDocumentCodec
    {
        public byte[] CanonicalBytes { get; } = [1, 2, 3];

        public byte[] Serialize(AIAssetDocument value) => CanonicalBytes.ToArray();

        public AIAssetDocument Deserialize(ReadOnlyMemory<byte> utf8Json) => document;

        public string SerializeToString(AIAssetDocument value) => throw new NotSupportedException();

        public AIAssetDocument Deserialize(string json) => throw new NotSupportedException();

        public ValueTask SerializeAsync(
            AIAssetDocument value,
            Stream output,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException(new NotSupportedException());

        public ValueTask<AIAssetDocument> DeserializeAsync(
            Stream input,
            CancellationToken cancellationToken = default)
            => ValueTask.FromException<AIAssetDocument>(new NotSupportedException());
    }

    private sealed class TestValidator : IAIAssetDocumentValidator
    {
        public ValueTask<AIAssetDocumentValidationResult> ValidateAsync(
            AIAssetDocument document,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(AIAssetDocumentValidationResult.Success());
    }

    private sealed class TestMapper : IAIAssetDocumentMapper
    {
        public Func<AIAssetDocument, IAsset>? Map { get; set; }

        public AIAssetDocument ToDocument(IAsset asset) => throw new NotSupportedException();

        public IAsset FromDocument(AIAssetDocument document)
            => Map?.Invoke(document) ?? new TestAsset();
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
