using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using PulseStack.Abstractions.Persistence.AIAssets.Storage;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;

namespace PulseStack.Core.Persistence.AIAssets.Storage;

public sealed class AIAssetWriter : IAIAssetWriter
{
    private readonly ISerializedAIAssetStore store;
    private readonly IAIAssetDocumentCodec codec;
    private readonly IAIAssetDocumentValidator validator;
    private readonly AIAssetStorageOptions options;

    public AIAssetWriter(
        ISerializedAIAssetStore store,
        IAIAssetDocumentCodec codec,
        IAIAssetDocumentValidator validator,
        AIAssetStorageOptions options)
    {
        this.store = store ?? throw CompositionFailure("A serialized AI Asset store is required.");
        this.codec = codec ?? throw CompositionFailure("An AI Asset document codec is required.");
        this.validator = validator ?? throw CompositionFailure("An AI Asset document validator is required.");
        AIAssetStorageContract.EnsureValidOptions(options);
        this.options = options;
    }

    public async ValueTask<AIAssetWriteResult> WriteAsync(
        AssetDefinitionKey key,
        AIAssetDocument document,
        CancellationToken cancellationToken = default)
    {
        AIAssetStorageContract.EnsureValidKey(key);
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var context = CreateContext(AIAssetStorageOperation.WriteDocument, key);
        await ValidateAsync(document, context, cancellationToken).ConfigureAwait(false);
        EnsureKeyAgreement(key, document, context);

        byte[] canonical;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            canonical = codec.Serialize(document);
        }
        catch (AIAssetDocumentCodecException exception)
        {
            throw new AIAssetStorageOperationException(
                "AI Asset document serialization failed.",
                context,
                exception);
        }

        EnsureWithinSizeLimit(canonical.Length, context);
        cancellationToken.ThrowIfCancellationRequested();
        return await WriteToStoreAsync(key, canonical, context, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<AIAssetWriteResult> WriteAsync(
        AssetDefinitionKey key,
        ReadOnlyMemory<byte> representation,
        CancellationToken cancellationToken = default)
    {
        AIAssetStorageContract.EnsureValidKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        var context = CreateContext(AIAssetStorageOperation.WriteRepresentation, key, representation.Length);
        AIAssetDocument document;
        try
        {
            document = codec.Deserialize(representation);
        }
        catch (AIAssetDocumentCodecException exception)
        {
            throw new AIAssetStorageOperationException(
                "AI Asset representation deserialization failed.",
                context,
                exception);
        }

        await ValidateAsync(document, context, cancellationToken).ConfigureAwait(false);
        EnsureKeyAgreement(key, document, context);

        byte[] canonical;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            canonical = codec.Serialize(document);
        }
        catch (AIAssetDocumentCodecException exception)
        {
            throw new AIAssetStorageOperationException(
                "AI Asset canonical reserialization failed.",
                context,
                exception);
        }

        if (!representation.Span.SequenceEqual(canonical))
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.NonCanonicalRepresentation,
                "The supplied serialized AI Asset representation is not canonical.",
                context);
        }

        EnsureWithinSizeLimit(canonical.Length, context);
        cancellationToken.ThrowIfCancellationRequested();
        return await WriteToStoreAsync(key, representation, context, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ValidateAsync(
        AIAssetDocument document,
        AIAssetStorageDiagnosticContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await validator.ValidateAsync(document, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.DocumentValidation,
                "AI Asset document validation failed.",
                context,
                result);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private async ValueTask<AIAssetWriteResult> WriteToStoreAsync(
        AssetDefinitionKey key,
        ReadOnlyMemory<byte> representation,
        AIAssetStorageDiagnosticContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await store.WriteAsync(key, representation, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.ProviderFailure,
                "The serialized AI Asset store failed while writing the representation.",
                context,
                innerException: exception);
        }
    }

    private void EnsureWithinSizeLimit(long size, AIAssetStorageDiagnosticContext context)
    {
        if (size > options.MaximumRepresentationSizeBytes)
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.RepresentationTooLarge,
                "The serialized AI Asset representation exceeds the configured maximum size.",
                context with
                {
                    RepresentationSizeBytes = size,
                    MaximumRepresentationSizeBytes = options.MaximumRepresentationSizeBytes
                });
        }
    }

    internal static void EnsureKeyAgreement(
        AssetDefinitionKey key,
        AIAssetDocument document,
        AIAssetStorageDiagnosticContext context)
    {
        var identity = document.Identity;
        if (!Guid.TryParse(identity.Id, out var id)
            || ToAssetType(document.AssetType) != key.Type
            || id != key.Id.Value
            || !string.Equals(identity.Version, key.Version.Value, StringComparison.Ordinal))
        {
            throw new AIAssetStorageException(
                AIAssetStorageFailureCategory.KeyDocumentMismatch,
                "The requested asset definition key does not match the AI Asset document identity.",
                context);
        }
    }

    private static AssetType ToAssetType(AIAssetDocumentType type) => type switch
    {
        AIAssetDocumentType.Project => AssetType.Project,
        AIAssetDocumentType.Library => AssetType.Library,
        AIAssetDocumentType.Package => AssetType.Package,
        AIAssetDocumentType.Workflow => AssetType.Workflow,
        AIAssetDocumentType.Agent => AssetType.Agent,
        AIAssetDocumentType.Prompt => AssetType.Prompt,
        AIAssetDocumentType.Tool => AssetType.Tool,
        AIAssetDocumentType.Knowledge => AssetType.Knowledge,
        AIAssetDocumentType.Memory => AssetType.Memory,
        AIAssetDocumentType.Policy => AssetType.Policy,
        AIAssetDocumentType.Model => AssetType.Model,
        _ => throw new InvalidOperationException("The validated AI Asset document type is unsupported.")
    };

    private AIAssetStorageDiagnosticContext CreateContext(
        AIAssetStorageOperation operation,
        AssetDefinitionKey key,
        long? representationSize = null) => new()
    {
        Operation = operation,
        Key = key,
        RepresentationSizeBytes = representationSize,
        MaximumRepresentationSizeBytes = options.MaximumRepresentationSizeBytes
    };

    private static AIAssetStorageException CompositionFailure(string message) => new(
        AIAssetStorageFailureCategory.CompositionConfiguration,
        message);
}
