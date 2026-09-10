using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Abstractions.Persistence.AIAssets.Serialization;

internal static class AIAssetDocumentRootDiscriminator
{
    internal const string MemberName = "assetType";

    internal static RootDescriptor ResolveForSerialization(AIAssetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var descriptor = ResolveDocumentType(document.GetType());

        if (document.AssetType != descriptor.AssetType)
        {
            throw new AIAssetDocumentCodecException(
                AIAssetDocumentCodecOperation.Serialization,
                AIAssetDocumentCodecFailureReason.DiscriminatorMismatch,
                $"Document type '{document.GetType().Name}' does not match its asset type discriminator.",
                MemberName,
                descriptor.Token);
        }

        return descriptor;
    }

    internal static RootDescriptor ResolveForDeserialization(string token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return token switch
        {
            "project" => new RootDescriptor("project", AIAssetDocumentType.Project, typeof(ProjectAssetDocument)),
            "library" => new RootDescriptor("library", AIAssetDocumentType.Library, typeof(LibraryAssetDocument)),
            "package" => new RootDescriptor("package", AIAssetDocumentType.Package, typeof(PackageAssetDocument)),
            "workflow" => new RootDescriptor("workflow", AIAssetDocumentType.Workflow, typeof(WorkflowAssetDocument)),
            "agent" => new RootDescriptor("agent", AIAssetDocumentType.Agent, typeof(AgentAssetDocument)),
            "prompt" => new RootDescriptor("prompt", AIAssetDocumentType.Prompt, typeof(PromptAssetDocument)),
            "tool" => new RootDescriptor("tool", AIAssetDocumentType.Tool, typeof(ToolAssetDocument)),
            "knowledge" => new RootDescriptor("knowledge", AIAssetDocumentType.Knowledge, typeof(KnowledgeAssetDocument)),
            "memory" => new RootDescriptor("memory", AIAssetDocumentType.Memory, typeof(MemoryAssetDocument)),
            "policy" => new RootDescriptor("policy", AIAssetDocumentType.Policy, typeof(PolicyAssetDocument)),
            "model" => new RootDescriptor("model", AIAssetDocumentType.Model, typeof(ModelAssetDocument)),
            "provider" => throw new AIAssetDocumentCodecException(
                AIAssetDocumentCodecOperation.Deserialization,
                AIAssetDocumentCodecFailureReason.UnsupportedDiscriminator,
                "The root asset discriminator 'provider' is reserved and unsupported in schema v1.",
                MemberName,
                token),
            _ => throw new AIAssetDocumentCodecException(
                AIAssetDocumentCodecOperation.Deserialization,
                AIAssetDocumentCodecFailureReason.UnknownDiscriminator,
                $"Unknown root asset discriminator '{token}'.",
                MemberName,
                token)
        };
    }

    private static RootDescriptor ResolveDocumentType(Type documentType)
    {
        if (documentType == typeof(ProjectAssetDocument))
        {
            return new RootDescriptor("project", AIAssetDocumentType.Project, documentType);
        }

        if (documentType == typeof(LibraryAssetDocument))
        {
            return new RootDescriptor("library", AIAssetDocumentType.Library, documentType);
        }

        if (documentType == typeof(PackageAssetDocument))
        {
            return new RootDescriptor("package", AIAssetDocumentType.Package, documentType);
        }

        if (documentType == typeof(WorkflowAssetDocument))
        {
            return new RootDescriptor("workflow", AIAssetDocumentType.Workflow, documentType);
        }

        if (documentType == typeof(AgentAssetDocument))
        {
            return new RootDescriptor("agent", AIAssetDocumentType.Agent, documentType);
        }

        if (documentType == typeof(PromptAssetDocument))
        {
            return new RootDescriptor("prompt", AIAssetDocumentType.Prompt, documentType);
        }

        if (documentType == typeof(ToolAssetDocument))
        {
            return new RootDescriptor("tool", AIAssetDocumentType.Tool, documentType);
        }

        if (documentType == typeof(KnowledgeAssetDocument))
        {
            return new RootDescriptor("knowledge", AIAssetDocumentType.Knowledge, documentType);
        }

        if (documentType == typeof(MemoryAssetDocument))
        {
            return new RootDescriptor("memory", AIAssetDocumentType.Memory, documentType);
        }

        if (documentType == typeof(PolicyAssetDocument))
        {
            return new RootDescriptor("policy", AIAssetDocumentType.Policy, documentType);
        }

        if (documentType == typeof(ModelAssetDocument))
        {
            return new RootDescriptor("model", AIAssetDocumentType.Model, documentType);
        }

        throw new AIAssetDocumentCodecException(
            AIAssetDocumentCodecOperation.Serialization,
            AIAssetDocumentCodecFailureReason.UnsupportedDocumentType,
            $"Document type '{documentType.FullName}' is not a supported schema-v1 root asset document.");
    }

    internal readonly record struct RootDescriptor(
        string Token,
        AIAssetDocumentType AssetType,
        Type DocumentType);
}
