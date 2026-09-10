using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Serialization;

internal static class AIAssetDocumentCanonicalJsonSerializer
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
        SkipValidation = false
    };

    internal static byte[] Serialize(AIAssetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var output = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(output, WriterOptions))
        {
            WriteDocument(writer, document);
        }

        return output.WrittenSpan.ToArray();
    }

    internal static string SerializeToString(AIAssetDocument document)
    {
        return StrictUtf8.GetString(Serialize(document));
    }

    private static void WriteDocument(Utf8JsonWriter writer, AIAssetDocument document)
    {
        var root = AIAssetDocumentRootDiscriminator.ResolveForSerialization(document);

        writer.WriteStartObject();
        writer.WriteString("assetType", root.Token);
        WriteDependencies(writer, document.Dependencies);

        switch (document)
        {
            case ProjectAssetDocument project:
                WriteReferenceProperty(writer, "entryWorkflow", project.EntryWorkflow);
                WriteIdentity(writer, project.Identity);
                WriteLifecycle(writer, project.Lifecycle);
                WriteMetadata(writer, project.Metadata);
                WriteReferenceArray(writer, "ownedAssets", project.OwnedAssets);
                WriteReferenceArray(writer, "references", project.References);
                WriteStringProperty(writer, "schemaVersion", project.SchemaVersion.Value);
                break;

            case LibraryAssetDocument library:
                WriteIdentity(writer, library.Identity);
                WriteLifecycle(writer, library.Lifecycle);
                WriteReferenceArray(writer, "members", library.Members);
                WriteMetadata(writer, library.Metadata);
                WriteReferenceArray(writer, "references", library.References);
                WriteStringProperty(writer, "schemaVersion", library.SchemaVersion.Value);
                break;

            case PackageAssetDocument package:
                WriteIdentity(writer, package.Identity);
                WriteLifecycle(writer, package.Lifecycle);
                WriteReferenceArray(writer, "members", package.Members);
                WriteMetadata(writer, package.Metadata);
                WriteReferenceArray(writer, "references", package.References);
                WriteStringProperty(writer, "schemaVersion", package.SchemaVersion.Value);
                break;

            case WorkflowAssetDocument workflow:
                WriteIdentity(writer, workflow.Identity);
                WriteLifecycle(writer, workflow.Lifecycle);
                WriteMetadata(writer, workflow.Metadata);
                WriteReferenceArray(writer, "references", workflow.References);
                WriteStringProperty(writer, "schemaVersion", workflow.SchemaVersion.Value);
                WriteWorkflowSteps(writer, workflow.Steps);
                break;

            case AgentAssetDocument agent:
                WriteStringProperty(writer, "goal", agent.Goal);
                WriteIdentity(writer, agent.Identity);
                WriteReferenceArray(writer, "knowledge", agent.Knowledge);
                WriteLifecycle(writer, agent.Lifecycle);
                WriteReferenceProperty(writer, "memory", agent.Memory);
                WriteMetadata(writer, agent.Metadata);
                WriteReferenceProperty(writer, "model", agent.Model);
                WriteReferenceArray(writer, "policies", agent.Policies);
                WriteReferenceProperty(writer, "prompt", agent.Prompt);
                WriteReferenceArray(writer, "references", agent.References);
                WriteStringArray(writer, "responsibilities", agent.Responsibilities);
                WriteStringProperty(writer, "role", agent.Role);
                WriteStringProperty(writer, "schemaVersion", agent.SchemaVersion.Value);
                WriteReferenceArray(writer, "tools", agent.Tools);
                break;

            case PromptAssetDocument prompt:
                WriteIdentity(writer, prompt.Identity);
                WriteLifecycle(writer, prompt.Lifecycle);
                WriteMetadata(writer, prompt.Metadata);
                WriteReferenceArray(writer, "references", prompt.References);
                WriteStringProperty(writer, "schemaVersion", prompt.SchemaVersion.Value);
                WriteStringProperty(writer, "systemInstructions", prompt.SystemInstructions);
                break;

            case ModelAssetDocument model:
                WriteIdentity(writer, model.Identity);
                WriteLifecycle(writer, model.Lifecycle);
                WriteMetadata(writer, model.Metadata);
                WriteStringProperty(writer, "model", model.Model);
                WriteStringProperty(writer, "provider", model.Provider);
                WriteReferenceArray(writer, "references", model.References);
                WriteStringProperty(writer, "schemaVersion", model.SchemaVersion.Value);
                break;

            case ToolAssetDocument:
            case KnowledgeAssetDocument:
            case MemoryAssetDocument:
            case PolicyAssetDocument:
                WriteIdentity(writer, document.Identity);
                WriteLifecycle(writer, document.Lifecycle);
                WriteMetadata(writer, document.Metadata);
                WriteReferenceArray(writer, "references", document.References);
                WriteStringProperty(writer, "schemaVersion", document.SchemaVersion.Value);
                break;

            default:
                throw new AIAssetDocumentCodecException(
                    AIAssetDocumentCodecOperation.Serialization,
                    AIAssetDocumentCodecFailureReason.UnsupportedDocumentType,
                    $"Document type '{document.GetType().FullName}' is not supported by canonical root serialization.");
        }

        writer.WriteEndObject();
    }

    private static void WriteDependencies(
        Utf8JsonWriter writer,
        IReadOnlyList<AIAssetDependencyDocument> dependencies)
    {
        writer.WritePropertyName("dependencies");
        writer.WriteStartArray();

        foreach (var dependency in dependencies)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("reference");
            WriteReference(writer, dependency.Reference);
            writer.WriteBoolean("required", dependency.Required);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteIdentity(Utf8JsonWriter writer, AIAssetIdentityDocument identity)
    {
        writer.WritePropertyName("identity");
        writer.WriteStartObject();
        WriteStringProperty(writer, "id", identity.Id);
        WriteStringProperty(writer, "urn", identity.Urn);
        WriteStringProperty(writer, "version", identity.Version);
        writer.WriteEndObject();
    }

    private static void WriteLifecycle(Utf8JsonWriter writer, AIAssetLifecycleDocument lifecycle)
    {
        var token = lifecycle switch
        {
            AIAssetLifecycleDocument.Draft => "draft",
            AIAssetLifecycleDocument.Validated => "validated",
            AIAssetLifecycleDocument.Published => "published",
            AIAssetLifecycleDocument.Deprecated => "deprecated",
            AIAssetLifecycleDocument.Archived => "archived",
            _ => throw UnrepresentableScalar("lifecycle", lifecycle.ToString())
        };

        writer.WriteString("lifecycle", token);
    }

    private static void WriteMetadata(Utf8JsonWriter writer, AIAssetMetadataDocument metadata)
    {
        writer.WritePropertyName("metadata");
        writer.WriteStartObject();
        WriteStringProperty(writer, "author", metadata.Author);
        WriteStringProperty(writer, "category", metadata.Category);
        WriteStringProperty(writer, "description", metadata.Description);
        WriteStringProperty(writer, "name", metadata.Name);
        WriteStringArray(writer, "tags", metadata.Tags);
        writer.WriteEndObject();
    }

    private static void WriteReferenceProperty(
        Utf8JsonWriter writer,
        string memberName,
        AIAssetReferenceDocument? reference)
    {
        writer.WritePropertyName(memberName);

        if (reference is null)
        {
            writer.WriteNullValue();
            return;
        }

        WriteReference(writer, reference);
    }

    private static void WriteReferenceArray(
        Utf8JsonWriter writer,
        string memberName,
        IReadOnlyList<AIAssetReferenceDocument> references)
    {
        writer.WritePropertyName(memberName);
        writer.WriteStartArray();

        foreach (var reference in references)
        {
            WriteReference(writer, reference);
        }

        writer.WriteEndArray();
    }

    private static void WriteReference(Utf8JsonWriter writer, AIAssetReferenceDocument reference)
    {
        writer.WriteStartObject();
        WriteStringProperty(writer, "assetId", reference.AssetId);
        writer.WriteString("assetType", GetReferenceAssetTypeToken(reference.AssetType));
        WriteStringProperty(writer, "urn", reference.Urn);
        WriteStringProperty(writer, "version", reference.Version);
        writer.WriteEndObject();
    }

    private static string GetReferenceAssetTypeToken(AIAssetDocumentType assetType)
    {
        return assetType switch
        {
            AIAssetDocumentType.Project => "project",
            AIAssetDocumentType.Library => "library",
            AIAssetDocumentType.Package => "package",
            AIAssetDocumentType.Workflow => "workflow",
            AIAssetDocumentType.Agent => "agent",
            AIAssetDocumentType.Prompt => "prompt",
            AIAssetDocumentType.Tool => "tool",
            AIAssetDocumentType.Knowledge => "knowledge",
            AIAssetDocumentType.Memory => "memory",
            AIAssetDocumentType.Policy => "policy",
            AIAssetDocumentType.Provider => "provider",
            AIAssetDocumentType.Model => "model",
            _ => throw UnrepresentableScalar("assetType", assetType.ToString())
        };
    }

    private static void WriteStringArray(
        Utf8JsonWriter writer,
        string memberName,
        IReadOnlyList<string> values)
    {
        writer.WritePropertyName(memberName);
        writer.WriteStartArray();

        foreach (var value in values)
        {
            WriteStringValue(writer, value, memberName);
        }

        writer.WriteEndArray();
    }

    private static void WriteStringProperty(
        Utf8JsonWriter writer,
        string memberName,
        string? value)
    {
        writer.WritePropertyName(memberName);
        WriteStringValue(writer, value, memberName);
    }

    private static void WriteStringValue(
        Utf8JsonWriter writer,
        string? value,
        string memberName)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        EnsureValidUnicode(value, memberName);
        writer.WriteStringValue(value);
    }

    private static void EnsureValidUnicode(string value, string memberName)
    {
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];

            if (char.IsHighSurrogate(current))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    throw InvalidUnicode(memberName);
                }

                index++;
                continue;
            }

            if (char.IsLowSurrogate(current))
            {
                throw InvalidUnicode(memberName);
            }
        }
    }

    private static void WriteWorkflowSteps(
        Utf8JsonWriter writer,
        IReadOnlyList<WorkflowStepDocument> steps)
    {
        writer.WritePropertyName("steps");
        writer.WriteStartArray();

        if (steps.Count != 0)
        {
            throw new InvalidOperationException(
                "Workflow step serialization is owned by MS-009.6B.4 and is not implemented by the B.3 root writer.");
        }

        writer.WriteEndArray();
    }

    private static AIAssetDocumentCodecException InvalidUnicode(string memberName)
    {
        return new AIAssetDocumentCodecException(
            AIAssetDocumentCodecOperation.Serialization,
            AIAssetDocumentCodecFailureReason.InvalidUnicode,
            $"Member '{memberName}' contains invalid Unicode scalar representation.",
            memberName);
    }

    private static AIAssetDocumentCodecException UnrepresentableScalar(
        string memberName,
        string token)
    {
        return new AIAssetDocumentCodecException(
            AIAssetDocumentCodecOperation.Serialization,
            AIAssetDocumentCodecFailureReason.UnrepresentableDocument,
            $"Member '{memberName}' cannot be represented by the schema-v1 canonical serialization profile.",
            memberName,
            token);
    }
}
