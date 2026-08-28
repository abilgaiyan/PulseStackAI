using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;

namespace PulseStack.Core.Persistence.AIAssets.Validation;

public sealed class AIAssetDocumentValidator : IAIAssetDocumentValidator
{
    public ValueTask<AIAssetDocumentValidationResult> ValidateAsync(
        AIAssetDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        cancellationToken.ThrowIfCancellationRequested();

        var errors = new List<AIAssetDocumentValidationError>();

        ValidateSchema(document, errors);
        ValidateIdentity(document.Identity, errors);
        ValidateMetadata(document.Metadata, errors);
        ValidateReferences(document.References, errors, cancellationToken);
        ValidateDependencies(document.Dependencies, errors, cancellationToken);
        ValidateAssetPayload(document, errors);

        return ValueTask.FromResult(new AIAssetDocumentValidationResult(errors));
    }

    private static void ValidateSchema(
        AIAssetDocument document,
        ICollection<AIAssetDocumentValidationError> errors)
    {
        if (document.SchemaVersion != AIAssetSchemaVersion.V1)
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.UnsupportedSchemaVersion,
                "The AI Asset document schema version is not supported.",
                "$.schemaVersion");
        }

        var hasDefinedAssetType = Enum.IsDefined(document.AssetType);

        if (!hasDefinedAssetType)
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.UnsupportedAssetType,
                "The AI Asset document type is not supported by this schema.",
                "$.assetType");
        }

        if (!TryGetExpectedAssetType(document, out var expectedAssetType)
            || (hasDefinedAssetType && document.AssetType != expectedAssetType))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.AssetTypeMismatch,
                "The AI Asset document implementation does not match its schema discriminator.",
                "$.assetType");
        }

        if (!Enum.IsDefined(document.Lifecycle))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.UnsupportedLifecycle,
                "The AI Asset document lifecycle value is not supported.",
                "$.lifecycle");
        }
    }

    private static bool TryGetExpectedAssetType(
        AIAssetDocument document,
        out AIAssetDocumentType assetType)
    {
        assetType = document switch
        {
            PromptAssetDocument => AIAssetDocumentType.Prompt,
            ToolAssetDocument => AIAssetDocumentType.Tool,
            KnowledgeAssetDocument => AIAssetDocumentType.Knowledge,
            MemoryAssetDocument => AIAssetDocumentType.Memory,
            PolicyAssetDocument => AIAssetDocumentType.Policy,
            ModelAssetDocument => AIAssetDocumentType.Model,
            _ => default
        };

        return document is PromptAssetDocument
            or ToolAssetDocument
            or KnowledgeAssetDocument
            or MemoryAssetDocument
            or PolicyAssetDocument
            or ModelAssetDocument;
    }

    private static void ValidateIdentity(
        AIAssetIdentityDocument? identity,
        ICollection<AIAssetDocumentValidationError> errors)
    {
        if (identity is null)
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.MissingIdentity,
                "The AI Asset document identity is required.",
                "$.identity");
            return;
        }

        if (!Guid.TryParse(identity.Id, out var id) || id == Guid.Empty)
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.InvalidIdentityId,
                "The AI Asset identity ID must be a non-empty GUID.",
                "$.identity.id");
        }

        if (string.IsNullOrWhiteSpace(identity.Urn))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.MissingIdentityUrn,
                "The AI Asset identity URN is required.",
                "$.identity.urn");
        }

        if (string.IsNullOrWhiteSpace(identity.Version))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.MissingIdentityVersion,
                "The AI Asset identity version is required.",
                "$.identity.version");
        }
    }

    private static void ValidateMetadata(
        AIAssetMetadataDocument? metadata,
        ICollection<AIAssetDocumentValidationError> errors)
    {
        if (metadata is null)
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.MissingMetadata,
                "The AI Asset document metadata is required.",
                "$.metadata");
            return;
        }

        if (string.IsNullOrWhiteSpace(metadata.Name))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.MissingMetadataName,
                "The AI Asset metadata name is required.",
                "$.metadata.name");
        }

        for (var index = 0; index < metadata.Tags.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(metadata.Tags[index]))
            {
                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.InvalidMetadataTag,
                    "AI Asset metadata tags cannot be empty or whitespace.",
                    $"$.metadata.tags[{index}]");
            }
        }
    }

    private static void ValidateReferences(
        IReadOnlyList<AIAssetReferenceDocument> references,
        ICollection<AIAssetDocumentValidationError> errors,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<ReferenceKey>();

        for (var index = 0; index < references.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reference = references[index];
            var path = $"$.references[{index}]";

            if (reference is null)
            {
                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.MissingReference,
                    "The AI Asset reference is required.",
                    path);
                continue;
            }

            ValidateReference(reference, path, errors);

            if (TryCreateReferenceKey(reference, out var key) && !seen.Add(key))
            {
                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.DuplicateReference,
                    "The AI Asset document contains a duplicate reference.",
                    path);
            }
        }
    }

    private static void ValidateDependencies(
        IReadOnlyList<AIAssetDependencyDocument> dependencies,
        ICollection<AIAssetDocumentValidationError> errors,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<ReferenceKey>();

        for (var index = 0; index < dependencies.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dependency = dependencies[index];
            var path = $"$.dependencies[{index}]";

            if (dependency is null)
            {
                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.MissingDependency,
                    "The AI Asset dependency is required.",
                    path);
                continue;
            }

            if (dependency.Reference is null)
            {
                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.MissingDependencyReference,
                    "The AI Asset dependency reference is required.",
                    $"{path}.reference");
                continue;
            }

            ValidateReference(dependency.Reference, $"{path}.reference", errors);

            if (TryCreateReferenceKey(dependency.Reference, out var key) && !seen.Add(key))
            {
                AddError(
                    errors,
                    AIAssetDocumentValidationCodes.DuplicateDependency,
                    "The AI Asset document contains a duplicate dependency.",
                    path);
            }
        }
    }

    private static void ValidateAssetPayload(
        AIAssetDocument document,
        ICollection<AIAssetDocumentValidationError> errors)
    {
        switch (document)
        {
            case PromptAssetDocument prompt:
                if (string.IsNullOrWhiteSpace(prompt.SystemInstructions))
                {
                    AddError(
                        errors,
                        AIAssetDocumentValidationCodes.MissingPromptSystemInstructions,
                        "Prompt system instructions are required.",
                        "$.systemInstructions");
                }

                break;

            case ToolAssetDocument tool:
                ValidateDescription(
                    tool.Metadata,
                    AIAssetDocumentValidationCodes.MissingToolDescription,
                    "Tool description is required.",
                    errors);

                if (string.IsNullOrWhiteSpace(tool.Metadata.Category))
                {
                    AddError(
                        errors,
                        AIAssetDocumentValidationCodes.MissingToolCategory,
                        "Tool category is required.",
                        "$.metadata.category");
                }

                break;

            case KnowledgeAssetDocument knowledge:
                ValidateDescription(
                    knowledge.Metadata,
                    AIAssetDocumentValidationCodes.MissingKnowledgeDescription,
                    "Knowledge description is required.",
                    errors);
                break;

            case MemoryAssetDocument memory:
                ValidateDescription(
                    memory.Metadata,
                    AIAssetDocumentValidationCodes.MissingMemoryDescription,
                    "Memory description is required.",
                    errors);
                break;

            case PolicyAssetDocument policy:
                ValidateDescription(
                    policy.Metadata,
                    AIAssetDocumentValidationCodes.MissingPolicyDescription,
                    "Policy description is required.",
                    errors);
                break;

            case ModelAssetDocument model:
                if (string.IsNullOrWhiteSpace(model.Provider))
                {
                    AddError(
                        errors,
                        AIAssetDocumentValidationCodes.MissingModelProvider,
                        "Model provider is required.",
                        "$.provider");
                }

                if (string.IsNullOrWhiteSpace(model.Model))
                {
                    AddError(
                        errors,
                        AIAssetDocumentValidationCodes.MissingModelName,
                        "Model name is required.",
                        "$.model");
                }

                break;
        }
    }

    private static void ValidateDescription(
        AIAssetMetadataDocument metadata,
        string code,
        string message,
        ICollection<AIAssetDocumentValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(metadata.Description))
        {
            AddError(errors, code, message, "$.metadata.description");
        }
    }

    private static void ValidateReference(
        AIAssetReferenceDocument reference,
        string path,
        ICollection<AIAssetDocumentValidationError> errors)
    {
        if (!Enum.IsDefined(reference.AssetType))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.UnsupportedReferenceAssetType,
                "The referenced AI Asset type is not supported by this schema.",
                $"{path}.assetType");
        }

        if (!Guid.TryParse(reference.AssetId, out var id) || id == Guid.Empty)
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.InvalidReferenceAssetId,
                "The referenced AI Asset ID must be a non-empty GUID.",
                $"{path}.assetId");
        }

        if (string.IsNullOrWhiteSpace(reference.Urn))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.MissingReferenceUrn,
                "The referenced AI Asset URN is required.",
                $"{path}.urn");
        }

        if (string.IsNullOrWhiteSpace(reference.Version))
        {
            AddError(
                errors,
                AIAssetDocumentValidationCodes.MissingReferenceVersion,
                "The referenced AI Asset version is required.",
                $"{path}.version");
        }
    }

    private static bool TryCreateReferenceKey(
        AIAssetReferenceDocument reference,
        out ReferenceKey key)
    {
        if (!Enum.IsDefined(reference.AssetType)
            || !Guid.TryParse(reference.AssetId, out var assetId)
            || assetId == Guid.Empty
            || string.IsNullOrWhiteSpace(reference.Version))
        {
            key = default;
            return false;
        }

        key = new ReferenceKey(
            reference.AssetType,
            assetId,
            reference.Version);
        return true;
    }

    private static void AddError(
        ICollection<AIAssetDocumentValidationError> errors,
        string code,
        string message,
        string path)
    {
        errors.Add(new AIAssetDocumentValidationError(code, message, path));
    }

    private readonly record struct ReferenceKey(
        AIAssetDocumentType AssetType,
        Guid AssetId,
        string Version);
}
