using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;

namespace PulseStack.Core.Persistence.AIAssets.Storage;

public sealed class AIAssetLoader : IAIAssetLoader
{
    private readonly ISerializedAIAssetStore store;
    private readonly IAIAssetDocumentCodec codec;
    private readonly IAIAssetDocumentValidator validator;
    private readonly IAIAssetDocumentMapper mapper;
    private readonly AIAssetStorageOptions options;

    public AIAssetLoader(
        ISerializedAIAssetStore store,
        IAIAssetDocumentCodec codec,
        IAIAssetDocumentValidator validator,
        IAIAssetDocumentMapper mapper,
        AIAssetStorageOptions options)
    {
        this.store = store ?? throw CompositionFailure("A serialized AI Asset store is required.");
        this.codec = codec ?? throw CompositionFailure("An AI Asset document codec is required.");
        this.validator = validator ?? throw CompositionFailure("An AI Asset document validator is required.");
        this.mapper = mapper ?? throw CompositionFailure("An AI Asset document mapper is required.");
        AIAssetStorageContract.EnsureValidOptions(options);
        this.options = options;
    }

    public async ValueTask<AIAssetLoadResult> LoadAsync(
        AssetDefinitionKey key,
        CancellationToken cancellationToken = default)
    {
        AIAssetStorageContract.EnsureValidKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        var context = new AIAssetStorageDiagnosticContext
        {
            Operation = AIAssetStorageOperation.Load,
            Key = key,
            MaximumRepresentationSizeBytes = options.MaximumRepresentationSizeBytes
        };

        var read = await ReadFromStoreAsync(key, context, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (read is SerializedAIAssetReadResult.NotFound)
        {
            return new AIAssetLoadResult.NotFound();
        }

        var representation = ((SerializedAIAssetReadResult.Found)read).Representation;
        context = context with { RepresentationSizeBytes = representation.Length };
        if (representation.Length > options.MaximumRepresentationSizeBytes)
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.RepresentationTooLarge,
                "The stored serialized AI Asset representation exceeds the configured maximum size.",
                context);
        }

        AIAssetDocument document;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            document = codec.Deserialize(representation);
        }
        catch (AIAssetDocumentCodecException exception)
        {
            throw new AIAssetStorageOperationException(
                "Stored AI Asset representation deserialization failed.",
                context,
                exception);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var validation = await validator.ValidateAsync(document, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.DocumentValidation,
                "Stored AI Asset document validation failed.",
                context,
                validation);
        }

        cancellationToken.ThrowIfCancellationRequested();
        AIAssetWriter.EnsureKeyAgreement(key, document, context);

        byte[] canonical;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            canonical = codec.Serialize(document);
        }
        catch (AIAssetDocumentCodecException exception)
        {
            throw new AIAssetStorageOperationException(
                "Stored AI Asset canonical reserialization failed.",
                context,
                exception);
        }

        if (!representation.Span.SequenceEqual(canonical))
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.NonCanonicalRepresentation,
                "The stored serialized AI Asset representation is not canonical.",
                context);
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var asset = mapper.FromDocument(document);
            return new AIAssetLoadResult.Loaded(asset);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.Mapping,
                "The validated AI Asset document could not be reconstructed.",
                context,
                innerException: exception);
        }
    }

    private async ValueTask<SerializedAIAssetReadResult> ReadFromStoreAsync(
        AssetDefinitionKey key,
        AIAssetStorageDiagnosticContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await store.ReadAsync(key, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.ProviderFailure,
                "The serialized AI Asset store failed while reading the representation.",
                context,
                innerException: exception);
        }
    }

    private static AIAssetStorageException CompositionFailure(string message) => new(
        AIAssetStorageFailureCategory.CompositionConfiguration,
        message);
}
