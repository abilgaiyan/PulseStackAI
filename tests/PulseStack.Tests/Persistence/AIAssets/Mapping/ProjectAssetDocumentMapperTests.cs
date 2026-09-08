using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Core.Assets;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class ProjectAssetDocumentMapperTests
{
    private readonly AIAssetDocumentMapper mapper = new();

    [Fact]
    public void ToDocument_ShouldMapProjectDocument_WithCanonicalProjectShape()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");
        var prompt = Reference(AssetType.Prompt, "prompt");
        var external = Reference(AssetType.Knowledge, "external");
        var project = CreateProject(
            entry,
            [agent, entry, prompt],
            [new AssetDependency(external, false)]);

        var document = mapper.ToDocument(project)
            .Should().BeOfType<ProjectAssetDocument>().Subject;

        document.SchemaVersion.Should().Be(AIAssetSchemaVersion.V1);
        document.AssetType.Should().Be(AIAssetDocumentType.Project);
        document.Identity.Id.Should().Be(project.Id.ToString());
        document.Identity.Urn.Should().Be(project.Urn.Value);
        document.Identity.Version.Should().Be(project.Version.Value);
        document.Metadata.Name.Should().Be(project.Options.Name);
        document.Metadata.Description.Should().Be(project.Options.Description);
        document.Lifecycle.Should().Be(AIAssetLifecycleDocument.Draft);

        document.EntryWorkflow.Should().BeEquivalentTo(ToDocument(entry));
        document.OwnedAssets.Should().Equal(
            ToDocument(agent),
            ToDocument(entry),
            ToDocument(prompt));
        document.References.Should().Equal(
            ToDocument(entry),
            ToDocument(agent),
            ToDocument(prompt));
        document.Dependencies.Should().ContainSingle();
        document.Dependencies[0].Reference.Should().BeEquivalentTo(ToDocument(external));
        document.Dependencies[0].Required.Should().BeFalse();
    }

    [Fact]
    public void RoundTrip_ProjectToDocumentToProject_ShouldPreserveEnvelopeAndProjectSemantics()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");
        var tool = Reference(AssetType.Tool, "tool");
        var external = Reference(AssetType.Model, "external-model");
        var original = CreateProject(
            entry,
            [tool, entry, agent],
            [new AssetDependency(external, false)]);

        var document = (ProjectAssetDocument)mapper.ToDocument(original);
        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<ProjectAsset>().Subject;

        reconstructed.Id.Should().Be(original.Id);
        reconstructed.Urn.Should().Be(original.Urn);
        reconstructed.Version.Should().Be(original.Version);
        reconstructed.Metadata.Should().Be(original.Metadata);
        reconstructed.Lifecycle.Should().Be(original.Lifecycle);
        reconstructed.Options.Name.Should().Be(original.Options.Name);
        reconstructed.Options.Description.Should().Be(original.Options.Description);
        reconstructed.Options.EntryWorkflow.Should().Be(entry);
        reconstructed.Options.OwnedAssets.Should().Equal(tool, entry, agent);
        reconstructed.References.Should().Equal(entry, tool, agent);
        reconstructed.Dependencies.Should().Equal(original.Dependencies);
    }

    [Fact]
    public void RoundTrip_ProjectToDocumentToProjectToDocument_ShouldPreserveDocument()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var memory = Reference(AssetType.Memory, "memory");
        var policy = Reference(AssetType.Policy, "policy");
        var external = Reference(AssetType.Knowledge, "external");
        var project = CreateProject(
            entry,
            [memory, entry, policy],
            [new AssetDependency(external)]);

        var first = mapper.ToDocument(project)
            .Should().BeOfType<ProjectAssetDocument>().Subject;
        var reconstructed = mapper.FromDocument(first);
        var second = mapper.ToDocument(reconstructed)
            .Should().BeOfType<ProjectAssetDocument>().Subject;

        second.Should().Be(first);
    }

    [Fact]
    public void ToDocument_ShouldRejectProjectWithCorruptedCommonReferences()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");
        var project = CreateProject(entry, [entry, agent]);
        var corrupted = project with { References = [] };

        var action = () => mapper.ToDocument(corrupted);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Project Asset References*canonical ownership projection*");
    }

    [Fact]
    public void ToDocument_ShouldRejectProjectWithMetadataThatDoesNotMatchOptions()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var project = CreateProject(entry, [entry]);
        var corrupted = project with
        {
            Metadata = project.Metadata with { Description = "Corrupted description" }
        };

        var action = () => mapper.ToDocument(corrupted);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Project Asset options field 'Description'*canonical Metadata*");
    }

    [Fact]
    public void FromDocument_ShouldRecomputeReferences_InsteadOfCopyingPersistedReferences()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");
        var project = CreateProject(entry, [agent, entry]);
        var canonical = (ProjectAssetDocument)mapper.ToDocument(project);
        var documentWithNonAuthoritativeReferences = new ProjectAssetDocument(
            canonical.SchemaVersion,
            canonical.Identity,
            canonical.Metadata,
            canonical.Lifecycle,
            canonical.EntryWorkflow,
            canonical.OwnedAssets,
            references: [],
            canonical.Dependencies);

        var reconstructed = mapper.FromDocument(documentWithNonAuthoritativeReferences)
            .Should().BeOfType<ProjectAsset>().Subject;

        reconstructed.References.Should().Equal(entry, agent);
    }

    [Fact]
    public void ProjectAssetDocument_ShouldPermitNullEntryWorkflow_ForLaterStructuralValidation()
    {
        var project = CreateProject(
            Reference(AssetType.Workflow, "entry"),
            []);
        var source = mapper.ToDocument(
            CreateProject(project.Options.EntryWorkflow, [project.Options.EntryWorkflow]))
            .Should().BeOfType<ProjectAssetDocument>().Subject;

        var malformed = new ProjectAssetDocument(
            AIAssetSchemaVersion.V1,
            source.Identity,
            source.Metadata,
            source.Lifecycle,
            entryWorkflow: null,
            source.OwnedAssets,
            source.References,
            source.Dependencies);

        malformed.AssetType.Should().Be(AIAssetDocumentType.Project);
        malformed.EntryWorkflow.Should().BeNull();
    }

    private static ProjectAsset CreateProject(
        AssetReference entry,
        IReadOnlyList<AssetReference> owned,
        IReadOnlyCollection<AssetDependency>? dependencies = null)
        => new ProjectAssetFactory().Create(
            new ProjectAssetOptions
            {
                Name = "Factory Intelligence",
                Description = "Project persistence fixture.",
                EntryWorkflow = entry,
                OwnedAssets = owned
            },
            dependencies);

    private static AssetReference Reference(AssetType type, string name)
    {
        var id = AssetId.New();
        return new AssetReference(
            type,
            id,
            new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{name}:{id}"),
            AssetVersion.Initial);
    }

    private static AIAssetReferenceDocument ToDocument(AssetReference reference)
        => new()
        {
            AssetType = reference.Type switch
            {
                AssetType.Workflow => AIAssetDocumentType.Workflow,
                AssetType.Agent => AIAssetDocumentType.Agent,
                AssetType.Prompt => AIAssetDocumentType.Prompt,
                AssetType.Tool => AIAssetDocumentType.Tool,
                AssetType.Knowledge => AIAssetDocumentType.Knowledge,
                AssetType.Memory => AIAssetDocumentType.Memory,
                AssetType.Policy => AIAssetDocumentType.Policy,
                AssetType.Model => AIAssetDocumentType.Model,
                _ => throw new InvalidOperationException($"Unsupported fixture type '{reference.Type}'.")
            },
            AssetId = reference.Id.ToString(),
            Urn = reference.Urn.Value,
            Version = reference.Version.Value
        };
}
