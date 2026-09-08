using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Core.Assets;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class LibraryAssetDocumentMapperTests
{
    private readonly AIAssetDocumentMapper mapper = new();

    [Fact]
    public void LibraryAssetDocument_ShouldSnapshotMembers_AndFixLibraryDiscriminator()
    {
        var member = Reference(AssetType.Agent, "agent");
        var source = mapper.ToDocument(CreateLibrary([member]))
            .Should().BeOfType<LibraryAssetDocument>().Subject;
        var members = source.Members.ToList();

        var document = new LibraryAssetDocument(
            AIAssetSchemaVersion.V1,
            source.Identity,
            source.Metadata,
            source.Lifecycle,
            members,
            source.References,
            source.Dependencies);

        members.Clear();

        document.AssetType.Should().Be(AIAssetDocumentType.Library);
        document.Members.Should().ContainSingle();
        document.Members[0].Should().BeEquivalentTo(ToDocument(member));
    }

    [Fact]
    public void RoundTrip_LibraryToDocumentToLibrary_ShouldPreserveEnvelopeAndLibrarySemantics()
    {
        var workflow = Reference(AssetType.Workflow, "workflow");
        var agent = Reference(AssetType.Agent, "agent");
        var prompt = Reference(AssetType.Prompt, "prompt");
        var external = Reference(AssetType.Model, "external-model");
        var library = CreateLibrary(
            [workflow, agent, prompt],
            [new AssetDependency(external, false)]);
        var original = library with
        {
            Version = new AssetVersion("2.3.0"),
            Lifecycle = AssetLifecycle.Published,
            Metadata = library.Metadata with
            {
                Author = "PulseStackAI Team",
                Category = "ReusableAssets",
                Tags = ["shared", "library"]
            }
        };

        var document = mapper.ToDocument(original)
            .Should().BeOfType<LibraryAssetDocument>().Subject;
        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<LibraryAsset>().Subject;

        reconstructed.Id.Should().Be(original.Id);
        reconstructed.Urn.Should().Be(original.Urn);
        reconstructed.Version.Should().Be(new AssetVersion("2.3.0"));
        reconstructed.Metadata.Name.Should().Be(original.Metadata.Name);
        reconstructed.Metadata.Description.Should().Be(original.Metadata.Description);
        reconstructed.Metadata.Author.Should().Be("PulseStackAI Team");
        reconstructed.Metadata.Category.Should().Be("ReusableAssets");
        reconstructed.Metadata.Tags.Should().Equal("shared", "library");
        reconstructed.Lifecycle.Should().Be(AssetLifecycle.Published);
        reconstructed.Options.Name.Should().Be(original.Options.Name);
        reconstructed.Options.Description.Should().Be(original.Options.Description);
        reconstructed.Options.Members.Should().Equal(workflow, agent, prompt);
        reconstructed.References.Should().Equal(workflow, agent, prompt);
        reconstructed.Dependencies.Should().Equal(original.Dependencies);

        mapper.ToDocument(reconstructed)
            .Should().BeOfType<LibraryAssetDocument>().Subject
            .Should().Be(document);
    }

    [Fact]
    public void ToDocument_ShouldRejectLibraryWithMetadataThatDoesNotMatchOptions()
    {
        var library = CreateLibrary([Reference(AssetType.Agent, "agent")]);
        var corrupted = library with
        {
            Metadata = library.Metadata with { Description = "Corrupted description" }
        };

        var action = () => mapper.ToDocument(corrupted);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Library Asset options field 'Description'*canonical Metadata*");
    }

    [Fact]
    public void ToDocument_ShouldRejectLibraryWithCorruptedCommonReferences()
    {
        var library = CreateLibrary([
            Reference(AssetType.Agent, "agent"),
            Reference(AssetType.Prompt, "prompt")]);
        var corrupted = library with { References = [] };

        var action = () => mapper.ToDocument(corrupted);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Library Asset References*canonical membership projection*");
    }

    [Fact]
    public void FromDocument_ShouldIgnorePersistedReferences_AndRecomputeCanonicalProjectionFromMembers()
    {
        var agent = Reference(AssetType.Agent, "agent");
        var prompt = Reference(AssetType.Prompt, "prompt");
        var source = mapper.ToDocument(CreateLibrary([agent, prompt]))
            .Should().BeOfType<LibraryAssetDocument>().Subject;
        var corruptedEnvelope = new LibraryAssetDocument(
            source.SchemaVersion,
            source.Identity,
            source.Metadata,
            source.Lifecycle,
            source.Members,
            references: [],
            source.Dependencies);

        var reconstructed = mapper.FromDocument(corruptedEnvelope)
            .Should().BeOfType<LibraryAsset>().Subject;

        reconstructed.Options.Members.Should().Equal(agent, prompt);
        reconstructed.References.Should().Equal(agent, prompt);
    }

    private static LibraryAsset CreateLibrary(
        IReadOnlyCollection<AssetReference> members,
        IReadOnlyCollection<AssetDependency>? dependencies = null)
        => new LibraryAssetFactory().Create(
            new LibraryAssetOptions
            {
                Name = "Shared Intelligence",
                Description = "Library persistence fixture.",
                Members = members
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
