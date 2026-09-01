using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;

namespace PulseStack.Core.Persistence.AIAssets.Mapping;

public sealed class AIAssetDocumentMapper : IAIAssetDocumentMapper
{
    public AIAssetDocument ToDocument(IAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        EnsureCanonicalMetadata(asset);

        var identity = new AIAssetIdentityDocument
        {
            Id = asset.Id.ToString(),
            Urn = asset.Urn.Value,
            Version = asset.Version.Value
        };
        var metadata = ToDocument(asset.Metadata);
        var lifecycle = ToDocument(asset.Lifecycle);
        var references = asset.References.Select(ToDocument).ToArray();
        var dependencies = asset.Dependencies.Select(ToDocument).ToArray();

        return asset switch
        {
            PromptAsset prompt => new PromptAssetDocument(
                AIAssetSchemaVersion.V1,
                identity,
                metadata,
                lifecycle,
                prompt.Options.SystemInstructions,
                references,
                dependencies),

            ToolAsset => new ToolAssetDocument(
                AIAssetSchemaVersion.V1,
                identity,
                metadata,
                lifecycle,
                references,
                dependencies),

            KnowledgeAsset => new KnowledgeAssetDocument(
                AIAssetSchemaVersion.V1,
                identity,
                metadata,
                lifecycle,
                references,
                dependencies),

            MemoryAsset => new MemoryAssetDocument(
                AIAssetSchemaVersion.V1,
                identity,
                metadata,
                lifecycle,
                references,
                dependencies),

            PolicyAsset => new PolicyAssetDocument(
                AIAssetSchemaVersion.V1,
                identity,
                metadata,
                lifecycle,
                references,
                dependencies),

            ModelAsset model => new ModelAssetDocument(
                AIAssetSchemaVersion.V1,
                identity,
                metadata,
                lifecycle,
                model.Options.Provider,
                model.Options.Model,
                references,
                dependencies),

            AgentDefinition agent => ToDocument(
                agent,
                identity,
                metadata,
                lifecycle,
                references,
                dependencies),

            _ => throw new NotSupportedException(
                $"Asset type '{asset.Type}' is not supported by the foundation Asset document mapper.")
        };
    }

    public IAsset FromDocument(AIAssetDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (document.SchemaVersion != AIAssetSchemaVersion.V1)
        {
            throw new NotSupportedException(
                $"AI Asset schema version '{document.SchemaVersion}' is not supported by the foundation Asset document mapper.");
        }

        var id = ParseAssetId(document.Identity.Id);
        var urn = new AssetUrn(Require(document.Identity.Urn, "Asset identity URN"));
        var version = new AssetVersion(Require(document.Identity.Version, "Asset identity version"));
        var metadata = FromDocument(document.Metadata);
        var lifecycle = FromDocument(document.Lifecycle);
        var references = document.References.Select(FromDocument).ToArray();
        var dependencies = document.Dependencies.Select(FromDocument).ToArray();

        return document switch
        {
            PromptAssetDocument prompt => FoundationAssetRehydrator.RehydratePrompt(
                id,
                urn,
                version,
                metadata,
                lifecycle,
                references,
                dependencies,
                new PromptAssetOptions
                {
                    Name = Require(metadata.Name, "Prompt name"),
                    SystemInstructions = Require(
                        prompt.SystemInstructions,
                        "Prompt system instructions")
                }),

            ToolAssetDocument => FoundationAssetRehydrator.RehydrateTool(
                id,
                urn,
                version,
                metadata,
                lifecycle,
                references,
                dependencies,
                new ToolAssetOptions
                {
                    Name = Require(metadata.Name, "Tool name"),
                    Description = Require(metadata.Description, "Tool description"),
                    Category = Require(metadata.Category, "Tool category"),
                    Tags = metadata.Tags.ToArray()
                }),

            KnowledgeAssetDocument => FoundationAssetRehydrator.RehydrateKnowledge(
                id,
                urn,
                version,
                metadata,
                lifecycle,
                references,
                dependencies,
                new KnowledgeAssetOptions
                {
                    Name = Require(metadata.Name, "Knowledge name"),
                    Description = Require(metadata.Description, "Knowledge description"),
                    Tags = metadata.Tags.ToArray()
                }),

            MemoryAssetDocument => FoundationAssetRehydrator.RehydrateMemory(
                id,
                urn,
                version,
                metadata,
                lifecycle,
                references,
                dependencies,
                new MemoryAssetOptions
                {
                    Name = Require(metadata.Name, "Memory name"),
                    Description = Require(metadata.Description, "Memory description"),
                    Tags = metadata.Tags.ToArray()
                }),

            PolicyAssetDocument => FoundationAssetRehydrator.RehydratePolicy(
                id,
                urn,
                version,
                metadata,
                lifecycle,
                references,
                dependencies,
                new PolicyAssetOptions
                {
                    Name = Require(metadata.Name, "Policy name"),
                    Description = Require(metadata.Description, "Policy description"),
                    Tags = metadata.Tags.ToArray()
                }),

            ModelAssetDocument model => FoundationAssetRehydrator.RehydrateModel(
                id,
                urn,
                version,
                metadata,
                lifecycle,
                references,
                dependencies,
                new ModelAssetOptions(
                    Require(model.Provider, "Model provider"),
                    Require(model.Model, "Model name"))),

            AgentAssetDocument agent => AgentDefinitionRehydrator.Rehydrate(
                id,
                urn,
                version,
                metadata,
                lifecycle,
                dependencies,
                new AgentDefinitionOptions
                {
                    Name = Require(metadata.Name, "Agent name"),
                    Goal = Require(agent.Goal, "Agent goal"),
                    Role = Require(agent.Role, "Agent role"),
                    Responsibilities = agent.Responsibilities.ToArray(),
                    Model = agent.Model is null ? null : FromDocument(agent.Model),
                    Prompt = agent.Prompt is null ? null : FromDocument(agent.Prompt),
                    Knowledge = agent.Knowledge.Select(FromDocument).ToArray(),
                    Tools = agent.Tools.Select(FromDocument).ToArray(),
                    Memory = agent.Memory is null ? null : FromDocument(agent.Memory),
                    Policies = agent.Policies.Select(FromDocument).ToArray()
                }),

            _ => throw new NotSupportedException(
                $"Document type '{document.AssetType}' is not supported by the foundation Asset document mapper.")
        };
    }

    private static AgentAssetDocument ToDocument(
        AgentDefinition agent,
        AIAssetIdentityDocument identity,
        AIAssetMetadataDocument metadata,
        AIAssetLifecycleDocument lifecycle,
        IReadOnlyList<AIAssetReferenceDocument> references,
        IReadOnlyList<AIAssetDependencyDocument> dependencies)
    {
        EnsureCanonicalAgentReferences(agent);

        return new AgentAssetDocument(
            AIAssetSchemaVersion.V1,
            identity,
            metadata,
            lifecycle,
            agent.Options.Goal,
            agent.Options.Role,
            agent.Options.Responsibilities,
            agent.Options.Model is null ? null : ToDocument(agent.Options.Model),
            agent.Options.Prompt is null ? null : ToDocument(agent.Options.Prompt),
            agent.Options.Knowledge.Select(ToDocument),
            agent.Options.Tools.Select(ToDocument),
            agent.Options.Memory is null ? null : ToDocument(agent.Options.Memory),
            agent.Options.Policies.Select(ToDocument),
            references,
            dependencies);
    }

    private static void EnsureCanonicalMetadata(IAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset.Metadata);

        switch (asset)
        {
            case PromptAsset prompt:
                EnsureEqual(prompt.Type, "Name", prompt.Options.Name, asset.Metadata.Name);
                break;

            case ToolAsset tool:
                EnsureEqual(tool.Type, "Name", tool.Options.Name, asset.Metadata.Name);
                EnsureEqual(tool.Type, "Description", tool.Options.Description, asset.Metadata.Description);
                EnsureEqual(tool.Type, "Category", tool.Options.Category, asset.Metadata.Category);
                EnsureTagsEqual(tool.Type, tool.Options.Tags, asset.Metadata.Tags);
                break;

            case KnowledgeAsset knowledge:
                EnsureEqual(knowledge.Type, "Name", knowledge.Options.Name, asset.Metadata.Name);
                EnsureEqual(knowledge.Type, "Description", knowledge.Options.Description, asset.Metadata.Description);
                EnsureTagsEqual(knowledge.Type, knowledge.Options.Tags, asset.Metadata.Tags);
                break;

            case MemoryAsset memory:
                EnsureEqual(memory.Type, "Name", memory.Options.Name, asset.Metadata.Name);
                EnsureEqual(memory.Type, "Description", memory.Options.Description, asset.Metadata.Description);
                EnsureTagsEqual(memory.Type, memory.Options.Tags, asset.Metadata.Tags);
                break;

            case PolicyAsset policy:
                EnsureEqual(policy.Type, "Name", policy.Options.Name, asset.Metadata.Name);
                EnsureEqual(policy.Type, "Description", policy.Options.Description, asset.Metadata.Description);
                EnsureTagsEqual(policy.Type, policy.Options.Tags, asset.Metadata.Tags);
                break;

            case AgentDefinition agent:
                EnsureEqual(agent.Type, "Name", agent.Options.Name, asset.Metadata.Name);
                break;
        }
    }

    private static void EnsureCanonicalAgentReferences(AgentDefinition agent)
    {
        var typedReferences = new List<AssetReference>();

        AddOptionalAgentReference(typedReferences, agent.Options.Model, AssetType.Model, "Model");
        AddOptionalAgentReference(typedReferences, agent.Options.Prompt, AssetType.Prompt, "Prompt");
        AddAgentReferences(typedReferences, agent.Options.Knowledge, AssetType.Knowledge, "Knowledge");
        AddAgentReferences(typedReferences, agent.Options.Tools, AssetType.Tool, "Tools");
        AddOptionalAgentReference(typedReferences, agent.Options.Memory, AssetType.Memory, "Memory");
        AddAgentReferences(typedReferences, agent.Options.Policies, AssetType.Policy, "Policies");

        if (!typedReferences.SequenceEqual(agent.References))
        {
            throw new InvalidOperationException(
                "Agent Asset typed references do not match the canonical common References projection.");
        }
    }

    private static void AddOptionalAgentReference(
        ICollection<AssetReference> target,
        AssetReference? reference,
        AssetType expectedType,
        string field)
    {
        if (reference is null)
        {
            return;
        }

        EnsureAgentReferenceType(reference, expectedType, field);
        target.Add(reference);
    }

    private static void AddAgentReferences(
        ICollection<AssetReference> target,
        IEnumerable<AssetReference> references,
        AssetType expectedType,
        string field)
    {
        var seen = new Dictionary<AssetDefinitionKey, AssetUrn>();

        foreach (var reference in references)
        {
            ArgumentNullException.ThrowIfNull(reference);
            EnsureAgentReferenceType(reference, expectedType, field);

            var definitionKey = AssetDefinitionKey.From(reference);
            if (seen.TryGetValue(definitionKey, out var existingUrn))
            {
                if (string.Equals(existingUrn.Value, reference.Urn.Value, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Agent Asset options field '{field}' contains a duplicate Asset definition reference.");
                }

                throw new InvalidOperationException(
                    $"Agent Asset options field '{field}' contains conflicting URNs for the same Asset definition identity.");
            }

            seen.Add(definitionKey, reference.Urn);
            target.Add(reference);
        }
    }

    private static void EnsureAgentReferenceType(
        AssetReference reference,
        AssetType expectedType,
        string field)
    {
        if (reference.Type != expectedType)
        {
            throw new InvalidOperationException(
                $"Agent Asset options field '{field}' must reference Asset type '{expectedType}'.");
        }
    }

    private static void EnsureEqual(
        AssetType assetType,
        string field,
        string? optionValue,
        string? metadataValue)
    {
        if (!string.Equals(optionValue, metadataValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{assetType} Asset options field '{field}' does not match canonical Metadata.");
        }
    }

    private static void EnsureTagsEqual(
        AssetType assetType,
        IEnumerable<string> optionTags,
        IEnumerable<string> metadataTags)
    {
        if (!optionTags.SequenceEqual(metadataTags, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"{assetType} Asset options field 'Tags' does not match canonical Metadata.");
        }
    }

    private static AIAssetMetadataDocument ToDocument(AssetMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new AIAssetMetadataDocument(
            metadata.Name,
            metadata.Description,
            metadata.Author,
            metadata.Tags,
            metadata.Category);
    }

    private static AssetMetadata FromDocument(AIAssetMetadataDocument metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new AssetMetadata
        {
            Name = Require(metadata.Name, "Asset metadata name"),
            Description = metadata.Description,
            Author = metadata.Author,
            Tags = metadata.Tags.ToArray(),
            Category = metadata.Category
        };
    }

    private static AIAssetReferenceDocument ToDocument(AssetReference reference)
        => new()
        {
            AssetType = ToDocument(reference.Type),
            AssetId = reference.Id.ToString(),
            Urn = reference.Urn.Value,
            Version = reference.Version.Value
        };

    private static AssetReference FromDocument(AIAssetReferenceDocument reference)
    {
        ArgumentNullException.ThrowIfNull(reference);

        return new AssetReference(
            FromDocument(reference.AssetType),
            ParseAssetId(reference.AssetId),
            new AssetUrn(Require(reference.Urn, "Asset reference URN")),
            new AssetVersion(Require(reference.Version, "Asset reference version")));
    }

    private static AIAssetDependencyDocument ToDocument(AssetDependency dependency)
        => new()
        {
            Reference = ToDocument(dependency.Reference),
            Required = dependency.Required
        };

    private static AssetDependency FromDocument(AIAssetDependencyDocument dependency)
    {
        ArgumentNullException.ThrowIfNull(dependency);
        ArgumentNullException.ThrowIfNull(dependency.Reference);

        return new AssetDependency(
            FromDocument(dependency.Reference),
            dependency.Required);
    }

    private static AIAssetDocumentType ToDocument(AssetType type)
        => type switch
        {
            AssetType.Project => AIAssetDocumentType.Project,
            AssetType.Library => AIAssetDocumentType.Library,
            AssetType.Package => AIAssetDocumentType.Package,
            AssetType.Workflow => AIAssetDocumentType.Workflow,
            AssetType.Agent => AIAssetDocumentType.Agent,
            AssetType.Prompt => AIAssetDocumentType.Prompt,
            AssetType.Tool => AIAssetDocumentType.Tool,
            AssetType.Knowledge => AIAssetDocumentType.Knowledge,
            AssetType.Memory => AIAssetDocumentType.Memory,
            AssetType.Policy => AIAssetDocumentType.Policy,
            AssetType.Provider => AIAssetDocumentType.Provider,
            AssetType.Model => AIAssetDocumentType.Model,
            _ => throw new NotSupportedException(
                $"Asset type '{type}' is not supported by AI Asset document schema v1.")
        };

    private static AssetType FromDocument(AIAssetDocumentType type)
        => type switch
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
            AIAssetDocumentType.Provider => AssetType.Provider,
            AIAssetDocumentType.Model => AssetType.Model,
            _ => throw new NotSupportedException(
                $"AI Asset document type '{type}' is not supported by schema v1 mapping.")
        };

    private static AIAssetLifecycleDocument ToDocument(AssetLifecycle lifecycle)
        => lifecycle switch
        {
            AssetLifecycle.Draft => AIAssetLifecycleDocument.Draft,
            AssetLifecycle.Validated => AIAssetLifecycleDocument.Validated,
            AssetLifecycle.Published => AIAssetLifecycleDocument.Published,
            AssetLifecycle.Deprecated => AIAssetLifecycleDocument.Deprecated,
            AssetLifecycle.Archived => AIAssetLifecycleDocument.Archived,
            _ => throw new NotSupportedException(
                $"Asset lifecycle '{lifecycle}' is not supported by schema v1 mapping.")
        };

    private static AssetLifecycle FromDocument(AIAssetLifecycleDocument lifecycle)
        => lifecycle switch
        {
            AIAssetLifecycleDocument.Draft => AssetLifecycle.Draft,
            AIAssetLifecycleDocument.Validated => AssetLifecycle.Validated,
            AIAssetLifecycleDocument.Published => AssetLifecycle.Published,
            AIAssetLifecycleDocument.Deprecated => AssetLifecycle.Deprecated,
            AIAssetLifecycleDocument.Archived => AssetLifecycle.Archived,
            _ => throw new NotSupportedException(
                $"AI Asset document lifecycle '{lifecycle}' is not supported by schema v1 mapping.")
        };

    private static AssetId ParseAssetId(string value)
    {
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Asset ID '{value}' is not a non-empty GUID.");
        }

        return new AssetId(id);
    }

    private static string Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{name} is required for mapping.");
        }

        return value;
    }
}
