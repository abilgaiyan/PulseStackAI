using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Core.Assets;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class AgentAssetDocumentMapperTests
{
    private readonly AIAssetDocumentMapper mapper = new();

    [Fact]
    public void RoundTrip_ShouldPreserveCompleteAgentDefinition()
    {
        var model = CreateReference(AssetType.Model, "model");
        var prompt = CreateReference(AssetType.Prompt, "prompt");
        var knowledge = CreateReference(AssetType.Knowledge, "knowledge");
        var tool = CreateReference(AssetType.Tool, "tool");
        var memory = CreateReference(AssetType.Memory, "memory");
        var policy = CreateReference(AssetType.Policy, "policy");
        var dependencyReference = CreateReference(AssetType.Prompt, "dependency");
        var source = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "Support Agent",
                Goal = "Resolve customer requests",
                Role = "Customer support specialist",
                Responsibilities = ["Understand intent", "Resolve accurately"],
                Model = model,
                Prompt = prompt,
                Knowledge = [knowledge],
                Tools = [tool],
                Memory = memory,
                Policies = [policy]
            }) with
        {
            Version = new AssetVersion("2.1.0"),
            Metadata = new AssetMetadata
            {
                Name = "Support Agent",
                Description = "Handles customer support requests.",
                Author = "PulseStackAI",
                Tags = ["support", "customer"],
                Category = "Business Agent"
            },
            Lifecycle = AssetLifecycle.Published,
            Dependencies = [new AssetDependency(dependencyReference, Required: false)]
        };

        var document = mapper.ToDocument(source);
        var restored = mapper.FromDocument(document).Should().BeOfType<AgentDefinition>().Subject;

        restored.Id.Should().Be(source.Id);
        restored.Urn.Should().Be(source.Urn);
        restored.Version.Should().Be(source.Version);
        restored.Metadata.Should().BeEquivalentTo(source.Metadata);
        restored.Lifecycle.Should().Be(source.Lifecycle);
        restored.Options.Name.Should().Be(source.Options.Name);
        restored.Options.Goal.Should().Be(source.Options.Goal);
        restored.Options.Role.Should().Be(source.Options.Role);
        restored.Options.Responsibilities.Should().Equal(source.Options.Responsibilities);
        restored.Options.Model.Should().Be(model);
        restored.Options.Prompt.Should().Be(prompt);
        restored.Options.Knowledge.Should().Equal(knowledge);
        restored.Options.Tools.Should().Equal(tool);
        restored.Options.Memory.Should().Be(memory);
        restored.Options.Policies.Should().Equal(policy);
        restored.References.Should().Equal(model, prompt, knowledge, tool, memory, policy);
        restored.Dependencies.Should().Equal(new AssetDependency(dependencyReference, Required: false));
    }

    [Fact]
    public void DocumentRoundTrip_ShouldPreserveCanonicalAgentDocument()
    {
        var model = CreateDocumentReference(AIAssetDocumentType.Model, "model");
        var knowledge = CreateDocumentReference(AIAssetDocumentType.Knowledge, "knowledge");
        var tool = CreateDocumentReference(AIAssetDocumentType.Tool, "tool");
        var policy = CreateDocumentReference(AIAssetDocumentType.Policy, "policy");
        var references = new[] { model, knowledge, tool, policy };
        var document = new AgentAssetDocument(
            AIAssetSchemaVersion.V1,
            CreateIdentity(),
            new AIAssetMetadataDocument(
                "Support Agent",
                "Handles customer requests.",
                "PulseStackAI",
                ["support"],
                "Business Agent"),
            AIAssetLifecycleDocument.Validated,
            "Resolve customer requests",
            "Customer support specialist",
            ["Understand intent", "Resolve accurately"],
            model: model,
            knowledge: [knowledge],
            tools: [tool],
            policies: [policy],
            references: references);

        var asset = mapper.FromDocument(document);
        var restored = mapper.ToDocument(asset);

        restored.Should().Be(document);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveDraftAgentWithoutModelPromptOrMemory()
    {
        var source = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "Draft Agent",
                Goal = "Explore a business task",
                Role = "Draft specialist"
            });

        var document = mapper.ToDocument(source).Should().BeOfType<AgentAssetDocument>().Subject;
        var restored = mapper.FromDocument(document).Should().BeOfType<AgentDefinition>().Subject;

        document.Model.Should().BeNull();
        document.Prompt.Should().BeNull();
        document.Memory.Should().BeNull();
        restored.Options.Model.Should().BeNull();
        restored.Options.Prompt.Should().BeNull();
        restored.Options.Memory.Should().BeNull();
    }

    [Fact]
    public void ToDocument_ShouldRejectAgent_WhenMetadataNameDiverges()
    {
        var source = CreateAgent();
        var asset = source with
        {
            Metadata = source.Metadata with { Name = "Different Agent" }
        };

        var action = () => mapper.ToDocument(asset);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Agent*'Name'*canonical Metadata*");
    }

    [Fact]
    public void ToDocument_ShouldRejectAgent_WhenEnvelopeReferencesDiverge()
    {
        var source = CreateAgent();
        var asset = source with { References = [] };

        var action = () => mapper.ToDocument(asset);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*typed references*canonical common References projection*");
    }

    [Fact]
    public void ToDocument_ShouldRejectAgent_WhenTypedCollectionContainsDuplicateDefinitionReference()
    {
        var tool = CreateReference(AssetType.Tool, "tool");
        var source = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "Duplicate Agent",
                Goal = "Exercise duplicate handling",
                Role = "Test specialist",
                Tools = [tool, tool]
            });

        var action = () => mapper.ToDocument(source);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*'Tools'*duplicate Asset definition reference*");
    }

    [Fact]
    public void ToDocument_ShouldRejectAgent_WhenTypedCollectionContainsConflictingUrnForDefinitionIdentity()
    {
        var tool = CreateReference(AssetType.Tool, "tool");
        var conflicting = tool with
        {
            Urn = new AssetUrn($"urn:pulsestack:tool:conflict:{tool.Id}")
        };
        var source = new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "Conflicting Agent",
                Goal = "Exercise identity conflict handling",
                Role = "Test specialist",
                Tools = [tool, conflicting]
            });

        var action = () => mapper.ToDocument(source);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*'Tools'*conflicting URNs*same Asset definition identity*");
    }

    [Fact]
    public async Task ToDocument_ShouldProduceAgentDocumentAcceptedByAggregateValidator()
    {
        var source = CreateAgent();
        var document = mapper.ToDocument(source).Should().BeOfType<AgentAssetDocument>().Subject;
        var validator = new AIAssetDocumentValidator();

        var result = await validator.ValidateAsync(document);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    private static AgentDefinition CreateAgent()
    {
        var model = CreateReference(AssetType.Model, "model");
        var tool = CreateReference(AssetType.Tool, "tool");

        return new AgentDefinitionFactory().Create(
            new AgentDefinitionOptions
            {
                Name = "Support Agent",
                Goal = "Resolve customer requests",
                Role = "Customer support specialist",
                Model = model,
                Tools = [tool]
            });
    }

    private static AssetReference CreateReference(AssetType type, string name)
    {
        var id = AssetId.New();
        return new AssetReference(
            type,
            id,
            new AssetUrn($"urn:pulsestack:{name}:{id}"),
            new AssetVersion("1.4.0"));
    }

    private static AIAssetReferenceDocument CreateDocumentReference(
        AIAssetDocumentType type,
        string name)
    {
        var id = Guid.NewGuid();
        return new AIAssetReferenceDocument
        {
            AssetType = type,
            AssetId = id.ToString(),
            Urn = $"urn:pulsestack:{name}:{id}",
            Version = "1.4.0"
        };
    }

    private static AIAssetIdentityDocument CreateIdentity()
    {
        var id = Guid.NewGuid();
        return new AIAssetIdentityDocument
        {
            Id = id.ToString(),
            Urn = $"urn:pulsestack:agent:{id}",
            Version = "2.1.0"
        };
    }
}
