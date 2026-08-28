using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Models;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Core.Assets;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class AIAssetDocumentMapperTests
{
    private readonly AIAssetDocumentMapper mapper = new();

    [Fact]
    public void RoundTrip_ShouldPreserveEveryFoundationAssetDefinition()
    {
        foreach (var asset in CreateFoundationAssets())
        {
            var document = mapper.ToDocument(asset);
            var restored = mapper.FromDocument(document);

            AssertCommonDefinition(asset, restored);
            AssertFoundationPayload(asset, restored);
        }
    }

    [Fact]
    public void RoundTrip_ShouldPreserveCommonEnvelopeReferencesAndDependencies()
    {
        var modelReference = CreateReference(AssetType.Model, "model");
        var promptReference = CreateReference(AssetType.Prompt, "prompt");
        var asset = new ToolAssetFactory().Create(
            new ToolAssetOptions
            {
                Name = "Search",
                Description = "Searches business data.",
                Category = "Retrieval",
                Tags = ["search", "business"]
            }) with
        {
            Version = new AssetVersion("2.4.0"),
            Metadata = new AssetMetadata
            {
                Name = "Search",
                Description = "Searches business data.",
                Author = "PulseStackAI",
                Tags = ["search", "business"],
                Category = "Retrieval"
            },
            Lifecycle = AssetLifecycle.Published,
            References = [modelReference],
            Dependencies = [new AssetDependency(promptReference, Required: false)]
        };

        var document = mapper.ToDocument(asset);
        var restored = mapper.FromDocument(document);

        document.SchemaVersion.Should().Be(AIAssetSchemaVersion.V1);
        document.Should().BeOfType<ToolAssetDocument>();
        AssertCommonDefinition(asset, restored);
        restored.References.Should().Equal(modelReference);
        restored.Dependencies.Should().Equal(
            new AssetDependency(promptReference, Required: false));
    }

    [Fact]
    public void ToDocument_ShouldRejectAssetOutsideFoundationMapperScope()
    {
        var asset = new TestAsset();

        var action = () => mapper.ToDocument(asset);

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*not supported by the foundation Asset document mapper*");
    }

    [Fact]
    public void FromDocument_ShouldRejectDocumentOutsideFoundationMapperScope()
    {
        var document = new TestAssetDocument();

        var action = () => mapper.FromDocument(document);

        action.Should().Throw<NotSupportedException>()
            .WithMessage("*not supported by the foundation Asset document mapper*");
    }

    private static IReadOnlyList<IAsset> CreateFoundationAssets()
    {
        var modelOptions = new ModelAssetOptions("TestProvider", "test-model");

        return
        [
            new PromptAssetFactory().Create(
                new PromptAssetOptions
                {
                    Name = "Assistant Prompt",
                    SystemInstructions = "Be precise."
                }),
            new ToolAssetFactory().Create(
                new ToolAssetOptions
                {
                    Name = "Calculator",
                    Description = "Performs calculations.",
                    Category = "Utilities",
                    Tags = ["math"]
                }),
            new KnowledgeAssetFactory().Create(
                new KnowledgeAssetOptions
                {
                    Name = "Customer Knowledge",
                    Description = "Customer reference material.",
                    Tags = ["customer"]
                }),
            new MemoryAssetFactory().Create(
                new MemoryAssetOptions
                {
                    Name = "Conversation Memory",
                    Description = "Retains conversation context.",
                    Tags = ["conversation"]
                }),
            new PolicyAssetFactory().Create(
                new PolicyAssetOptions
                {
                    Name = "Privacy Policy",
                    Description = "Protects customer data.",
                    Tags = ["privacy"]
                }),
            new ModelAssetFactory(new TestModelCatalog(modelOptions)).Create(modelOptions)
        ];
    }

    private static void AssertCommonDefinition(IAsset expected, IAsset actual)
    {
        actual.Type.Should().Be(expected.Type);
        actual.Id.Should().Be(expected.Id);
        actual.Urn.Should().Be(expected.Urn);
        actual.Version.Should().Be(expected.Version);
        actual.Lifecycle.Should().Be(expected.Lifecycle);
        actual.Metadata.Name.Should().Be(expected.Metadata.Name);
        actual.Metadata.Description.Should().Be(expected.Metadata.Description);
        actual.Metadata.Author.Should().Be(expected.Metadata.Author);
        actual.Metadata.Category.Should().Be(expected.Metadata.Category);
        actual.Metadata.Tags.Should().Equal(expected.Metadata.Tags);
        actual.References.Should().Equal(expected.References);
        actual.Dependencies.Should().Equal(expected.Dependencies);
    }

    private static void AssertFoundationPayload(IAsset expected, IAsset actual)
    {
        switch (expected, actual)
        {
            case (PromptAsset source, PromptAsset restored):
                restored.Options.Name.Should().Be(source.Options.Name);
                restored.Options.SystemInstructions.Should().Be(source.Options.SystemInstructions);
                break;

            case (ToolAsset source, ToolAsset restored):
                restored.Options.Name.Should().Be(source.Options.Name);
                restored.Options.Description.Should().Be(source.Options.Description);
                restored.Options.Category.Should().Be(source.Options.Category);
                restored.Options.Tags.Should().Equal(source.Options.Tags);
                break;

            case (KnowledgeAsset source, KnowledgeAsset restored):
                restored.Options.Name.Should().Be(source.Options.Name);
                restored.Options.Description.Should().Be(source.Options.Description);
                restored.Options.Tags.Should().Equal(source.Options.Tags);
                break;

            case (MemoryAsset source, MemoryAsset restored):
                restored.Options.Name.Should().Be(source.Options.Name);
                restored.Options.Description.Should().Be(source.Options.Description);
                restored.Options.Tags.Should().Equal(source.Options.Tags);
                break;

            case (PolicyAsset source, PolicyAsset restored):
                restored.Options.Name.Should().Be(source.Options.Name);
                restored.Options.Description.Should().Be(source.Options.Description);
                restored.Options.Tags.Should().Equal(source.Options.Tags);
                break;

            case (ModelAsset source, ModelAsset restored):
                restored.Options.Provider.Should().Be(source.Options.Provider);
                restored.Options.Model.Should().Be(source.Options.Model);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unexpected foundation Asset pair '{expected.GetType().Name}' and '{actual.GetType().Name}'.");
        }
    }

    private static AssetReference CreateReference(AssetType type, string name)
    {
        var id = AssetId.New();
        return new AssetReference(
            type,
            id,
            new AssetUrn($"urn:pulsestack:{name}:{id}"),
            new AssetVersion("3.0.0"));
    }

    private sealed class TestModelCatalog(params ModelAssetOptions[] models) : IModelCatalog
    {
        public IReadOnlyCollection<ProviderModelDescriptor> GetModels()
            => models
                .Select(model => new ProviderModelDescriptor(model.Provider, model.Model))
                .ToArray();

        public bool Contains(string provider, string model)
            => models.Any(candidate =>
                string.Equals(candidate.Provider, provider, StringComparison.Ordinal)
                && string.Equals(candidate.Model, model, StringComparison.Ordinal));
    }

    private sealed record TestAsset : Asset
    {
        [SetsRequiredMembers]
        public TestAsset()
            : base(AssetType.Agent)
        {
            var id = AssetId.New();
            Id = id;
            Urn = new AssetUrn($"urn:pulsestack:agent:{id}");
            Version = AssetVersion.Initial;
            Metadata = new AssetMetadata { Name = "Agent" };
            Lifecycle = AssetLifecycle.Draft;
        }
    }

    private sealed record TestAssetDocument : AIAssetDocument
    {
        public TestAssetDocument()
            : base(
                AIAssetSchemaVersion.V1,
                AIAssetDocumentType.Agent,
                new AIAssetIdentityDocument
                {
                    Id = Guid.NewGuid().ToString(),
                    Urn = "urn:pulsestack:agent:test",
                    Version = "1.0.0"
                },
                new AIAssetMetadataDocument("Agent"),
                AIAssetLifecycleDocument.Draft)
        {
        }
    }
}
