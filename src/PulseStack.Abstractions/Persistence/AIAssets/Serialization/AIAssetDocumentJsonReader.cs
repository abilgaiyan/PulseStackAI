using System.Text.Json;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Serialization;

internal static class AIAssetDocumentJsonReader
{
    internal static AIAssetDocument Read(AIAssetDocumentStrictJsonDeserializer.ParsedRoot parsed)
    {
        var root = parsed.Root;
        var common = ReadCommon(root);

        return parsed.Descriptor.AssetType switch
        {
            AIAssetDocumentType.Project => new ProjectAssetDocument(common.Version, common.Identity, common.Metadata, common.Lifecycle,
                ReadNullableReference(root, "entryWorkflow"), ReadReferenceArray(root, "ownedAssets"), common.References, common.Dependencies),
            AIAssetDocumentType.Library => new LibraryAssetDocument(common.Version, common.Identity, common.Metadata, common.Lifecycle,
                ReadReferenceArray(root, "members"), common.References, common.Dependencies),
            AIAssetDocumentType.Package => new PackageAssetDocument(common.Version, common.Identity, common.Metadata, common.Lifecycle,
                ReadReferenceArray(root, "members"), common.References, common.Dependencies),
            AIAssetDocumentType.Workflow => new WorkflowAssetDocument(common.Version, common.Identity, common.Metadata, common.Lifecycle,
                WorkflowDocumentJsonReader.ReadSteps(Required(root, "steps")), common.References, common.Dependencies),
            AIAssetDocumentType.Agent => new AgentAssetDocument(common.Version, common.Identity, common.Metadata, common.Lifecycle,
                ReadNullableString(root, "goal")!, ReadNullableString(root, "role")!, ReadStringArray(root, "responsibilities"),
                ReadNullableReference(root, "model"), ReadNullableReference(root, "prompt"), ReadReferenceArray(root, "knowledge"),
                ReadReferenceArray(root, "tools"), ReadNullableReference(root, "memory"), ReadReferenceArray(root, "policies"),
                common.References, common.Dependencies),
            AIAssetDocumentType.Prompt => new PromptAssetDocument(common.Version, common.Identity, common.Metadata, common.Lifecycle,
                ReadNullableString(root, "systemInstructions")!, common.References, common.Dependencies),
            AIAssetDocumentType.Model => new ModelAssetDocument(common.Version, common.Identity, common.Metadata, common.Lifecycle,
                ReadNullableString(root, "provider")!, ReadNullableString(root, "model")!, common.References, common.Dependencies),
            AIAssetDocumentType.Tool => new ToolAssetDocument(common.Version, common.Identity, common.Metadata, common.Lifecycle, common.References, common.Dependencies),
            AIAssetDocumentType.Knowledge => new KnowledgeAssetDocument(common.Version, common.Identity, common.Metadata, common.Lifecycle, common.References, common.Dependencies),
            AIAssetDocumentType.Memory => new MemoryAssetDocument(common.Version, common.Identity, common.Metadata, common.Lifecycle, common.References, common.Dependencies),
            AIAssetDocumentType.Policy => new PolicyAssetDocument(common.Version, common.Identity, common.Metadata, common.Lifecycle, common.References, common.Dependencies),
            _ => throw Codec(AIAssetDocumentCodecFailureReason.UnsupportedDocumentType, "The root document type is unsupported.")
        };
    }

    private static Common ReadCommon(JsonElement root) => new(
        AIAssetSchemaVersion.V1,
        ReadIdentity(Required(root, "identity")),
        ReadMetadata(Required(root, "metadata")),
        ReadLifecycle(Required(root, "lifecycle")),
        ReadReferenceArray(root, "references"),
        ReadDependencies(Required(root, "dependencies")));

    private static AIAssetIdentityDocument ReadIdentity(JsonElement element)
    {
        EnsureObject(element, "identity", ["id", "urn", "version"], ["id", "urn", "version"]);
        return new AIAssetIdentityDocument
        {
            Id = ReadNullableString(element, "id")!,
            Urn = ReadNullableString(element, "urn")!,
            Version = ReadNullableString(element, "version")!
        };
    }

    private static AIAssetMetadataDocument ReadMetadata(JsonElement element)
    {
        EnsureObject(element, "metadata", ["author", "category", "description", "name", "tags"], ["author", "category", "description", "name", "tags"]);
        return new AIAssetMetadataDocument(
            ReadNullableString(element, "name")!,
            ReadNullableString(element, "description"),
            ReadNullableString(element, "author"),
            ReadStringArray(element, "tags"),
            ReadNullableString(element, "category"));
    }

    private static AIAssetLifecycleDocument ReadLifecycle(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
            throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'lifecycle' must be a JSON string.", "lifecycle");
        var token = element.GetString()!;
        return token switch
        {
            "draft" => AIAssetLifecycleDocument.Draft,
            "validated" => AIAssetLifecycleDocument.Validated,
            "published" => AIAssetLifecycleDocument.Published,
            "deprecated" => AIAssetLifecycleDocument.Deprecated,
            "archived" => AIAssetLifecycleDocument.Archived,
            _ => throw Codec(AIAssetDocumentCodecFailureReason.InvalidScalarToken, $"Unknown lifecycle token '{token}'.", "lifecycle", token)
        };
    }

    private static IReadOnlyList<AIAssetDependencyDocument> ReadDependencies(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'dependencies' must be an array.", "dependencies");
        var result = new List<AIAssetDependencyDocument>();
        foreach (var item in element.EnumerateArray())
        {
            EnsureObject(item, "dependencies", ["reference", "required"], ["reference", "required"]);
            var required = Required(item, "required");
            if (required.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
                throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Member 'required' must be boolean.", "required");
            result.Add(new AIAssetDependencyDocument { Reference = ReadReference(Required(item, "reference")), Required = required.GetBoolean() });
        }
        return result;
    }

    internal static AIAssetReferenceDocument ReadReference(JsonElement element)
    {
        EnsureObject(element, "reference", ["assetId", "assetType", "urn", "version"], ["assetId", "assetType", "urn", "version"]);
        var typeElement = Required(element, "assetType");
        if (typeElement.ValueKind != JsonValueKind.String)
            throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, "Reference assetType must be a string.", "assetType");
        var token = typeElement.GetString()!;
        var type = token switch
        {
            "project" => AIAssetDocumentType.Project, "library" => AIAssetDocumentType.Library, "package" => AIAssetDocumentType.Package,
            "workflow" => AIAssetDocumentType.Workflow, "agent" => AIAssetDocumentType.Agent, "prompt" => AIAssetDocumentType.Prompt,
            "tool" => AIAssetDocumentType.Tool, "knowledge" => AIAssetDocumentType.Knowledge, "memory" => AIAssetDocumentType.Memory,
            "policy" => AIAssetDocumentType.Policy, "provider" => AIAssetDocumentType.Provider, "model" => AIAssetDocumentType.Model,
            _ => throw Codec(AIAssetDocumentCodecFailureReason.InvalidScalarToken, $"Unknown reference assetType token '{token}'.", "assetType", token)
        };
        return new AIAssetReferenceDocument
        {
            AssetType = type,
            AssetId = ReadNullableString(element, "assetId")!,
            Urn = ReadNullableString(element, "urn")!,
            Version = ReadNullableString(element, "version")!
        };
    }

    internal static JsonElement Required(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var value))
            throw Codec(AIAssetDocumentCodecFailureReason.MissingRequiredMember, $"Required member '{name}' is missing.", name);
        return value;
    }

    internal static string? ReadNullableString(JsonElement obj, string name)
    {
        var value = Required(obj, name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String)
            throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Member '{name}' must be a string or null.", name);
        return value.GetString();
    }

    internal static IReadOnlyList<string> ReadStringArray(JsonElement obj, string name)
    {
        var value = Required(obj, name);
        if (value.ValueKind != JsonValueKind.Array)
            throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Member '{name}' must be an array.", name);
        var result = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Null) { result.Add(null!); continue; }
            if (item.ValueKind != JsonValueKind.String)
                throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Items in '{name}' must be strings or null.", name);
            result.Add(item.GetString()!);
        }
        return result;
    }

    internal static IReadOnlyList<AIAssetReferenceDocument> ReadReferenceArray(JsonElement obj, string name)
    {
        var value = Required(obj, name);
        if (value.ValueKind != JsonValueKind.Array)
            throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Member '{name}' must be an array.", name);
        return value.EnumerateArray().Select(ReadReference).ToArray();
    }

    private static AIAssetReferenceDocument? ReadNullableReference(JsonElement obj, string name)
    {
        var value = Required(obj, name);
        return value.ValueKind == JsonValueKind.Null ? null : ReadReference(value);
    }

    internal static void EnsureObject(JsonElement element, string context, IReadOnlyCollection<string> allowed, IReadOnlyCollection<string> required)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Codec(AIAssetDocumentCodecFailureReason.InvalidTokenKind, $"Member '{context}' must be an object.", context);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
                throw Codec(AIAssetDocumentCodecFailureReason.DuplicateMember, $"Duplicate member '{property.Name}'.", property.Name);
            if (!allowed.Contains(property.Name, StringComparer.Ordinal))
                throw Codec(AIAssetDocumentCodecFailureReason.UnknownMember, $"Unknown member '{property.Name}'.", property.Name);
        }
        foreach (var name in required)
            if (!seen.Contains(name)) throw Codec(AIAssetDocumentCodecFailureReason.MissingRequiredMember, $"Required member '{name}' is missing.", name);
    }

    internal static AIAssetDocumentCodecException Codec(AIAssetDocumentCodecFailureReason reason, string message, string? member = null, string? token = null, Exception? inner = null) =>
        new(AIAssetDocumentCodecOperation.Deserialization, reason, message, member, token, inner);

    private readonly record struct Common(AIAssetSchemaVersion Version, AIAssetIdentityDocument Identity,
        AIAssetMetadataDocument Metadata, AIAssetLifecycleDocument Lifecycle,
        IReadOnlyList<AIAssetReferenceDocument> References, IReadOnlyList<AIAssetDependencyDocument> Dependencies);
}
