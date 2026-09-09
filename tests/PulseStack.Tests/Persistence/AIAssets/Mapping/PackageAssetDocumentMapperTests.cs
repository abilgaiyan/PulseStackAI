using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Core.Assets;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class PackageAssetDocumentMapperTests
{
    private readonly AIAssetDocumentMapper mapper = new();

    [Fact]
    public void PackageAssetDocument_ShouldSnapshotMembers_AndFixPackageDiscriminator()
    {
        var member = Reference(AssetType.Agent, "agent");
        var source = mapper.ToDocument(CreatePackage([member]))
            .Should().BeOfType<PackageAssetDocument>().Subject;
        var members = source.Members.ToList();

        var document = new PackageAssetDocument(
            AIAssetSchemaVersion.V1,
            source.Identity,
            source.Metadata,
            source.Lifecycle,
            members,
            source.References,
            source.Dependencies);

        members.Clear();

        document.AssetType.Should().Be(AIAssetDocumentType.Package);
        document.Members.Should().ContainSingle();
        document.Members[0].Should().Be(source.Members[0]);
    }

    [Fact]
    public void RoundTrip_PackageToDocumentToPackage_ShouldPreserveEnvelopeAndPackageSemantics()
    {
        var workflow = Reference(AssetType.Workflow, "workflow");
        var library = Reference(AssetType.Library, "library");
        var nestedPackage = Reference(AssetType.Package, "nested-package");
        var external = Reference(AssetType.Provider, "external-provider");
        var package = CreatePackage(
            [workflow, library, nestedPackage],
            [new AssetDependency(external, false)]);
        var original = package with
        {
            Version = new AssetVersion("2.3.0"),
            Lifecycle = AssetLifecycle.Published,
            Metadata = package.Metadata with
            {
                Author = "PulseStackAI Team",
                Category = "Distribution",
                Tags = ["portable", "package"]
            }
        };

        var document = mapper.ToDocument(original)
            .Should().BeOfType<PackageAssetDocument>().Subject;
        var reconstructed = mapper.FromDocument(document)
            .Should().BeOfType<PackageAsset>().Subject;

        reconstructed.Id.Should().Be(original.Id);
        reconstructed.Urn.Should().Be(original.Urn);
        reconstructed.Version.Should().Be(new AssetVersion("2.3.0"));
        reconstructed.Metadata.Name.Should().Be(original.Metadata.Name);
        reconstructed.Metadata.Description.Should().Be(original.Metadata.Description);
        reconstructed.Metadata.Author.Should().Be("PulseStackAI Team");
        reconstructed.Metadata.Category.Should().Be("Distribution");
        reconstructed.Metadata.Tags.Should().Equal("portable", "package");
        reconstructed.Lifecycle.Should().Be(AssetLifecycle.Published);
        reconstructed.Options.Name.Should().Be(original.Options.Name);
        reconstructed.Options.Description.Should().Be(original.Options.Description);
        reconstructed.Options.Members.Should().Equal(workflow, library, nestedPackage);
        reconstructed.References.Should().Equal(workflow, library, nestedPackage);
        reconstructed.Dependencies.Should().Equal(original.Dependencies);

        mapper.ToDocument(reconstructed)
            .Should().BeOfType<PackageAssetDocument>().Subject
            .Should().Be(document);
    }

    [Fact]
    public void ToDocument_ShouldRejectPackageWithMetadataThatDoesNotMatchOptions()
    {
        var package = CreatePackage([Reference(AssetType.Agent, "agent")]);
        var corrupted = package with
        {
            Metadata = package.Metadata with { Description = "Corrupted description" }
        };

        var action = () => mapper.ToDocument(corrupted);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Package Asset options field 'Description'*canonical Metadata*");
    }

    [Fact]
    public void ToDocument_ShouldRejectPackageWithCorruptedCommonReferences()
    {
        var package = CreatePackage([
            Reference(AssetType.Agent, "agent"),
            Reference(AssetType.Prompt, "prompt")]);
        var corrupted = package with { References = [] };

        var action = () => mapper.ToDocument(corrupted);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Package Asset References*canonical membership projection*");
    }

    [Fact]
    public void FromDocument_ShouldIgnorePersistedReferences_AndRecomputeCanonicalProjectionFromMembers()
    {
        var agent = Reference(AssetType.Agent, "agent");
        var prompt = Reference(AssetType.Prompt, "prompt");
        var source = mapper.ToDocument(CreatePackage([agent, prompt]))
            .Should().BeOfType<PackageAssetDocument>().Subject;
        var corruptedEnvelope = new PackageAssetDocument(
            source.SchemaVersion,
            source.Identity,
            source.Metadata,
            source.Lifecycle,
            source.Members,
            references: [],
            dependencies: source.Dependencies);

        var reconstructed = mapper.FromDocument(corruptedEnvelope)
            .Should().BeOfType<PackageAsset>().Subject;

        reconstructed.Options.Members.Should().Equal(agent, prompt);
        reconstructed.References.Should().Equal(agent, prompt);
    }

    [Fact]
    public void FromDocument_ShouldUsePersistedPackageVersionForExactSelfIdentity()
    {
        var source = mapper.ToDocument(CreatePackage([Reference(AssetType.Agent, "agent")]))
            .Should().BeOfType<PackageAssetDocument>().Subject;
        var persistedVersion = new AssetVersion("2.0.0");
        var olderSameIdentityMember = new AIAssetReferenceDocument
        {
            AssetType = AIAssetDocumentType.Package,
            AssetId = source.Identity.Id,
            Urn = source.Identity.Urn,
            Version = AssetVersion.Initial.Value
        };
        var versionedDocument = new PackageAssetDocument(
            source.SchemaVersion,
            source.Identity with { Version = persistedVersion.Value },
            source.Metadata,
            source.Lifecycle,
            members: [olderSameIdentityMember],
            references: [],
            dependencies: []);

        var reconstructed = mapper.FromDocument(versionedDocument)
            .Should().BeOfType<PackageAsset>().Subject;

        reconstructed.Version.Should().Be(persistedVersion);
        reconstructed.Options.Members.Should().ContainSingle()
            .Which.Version.Should().Be(AssetVersion.Initial);
        reconstructed.References.Should().Equal(reconstructed.Options.Members);
    }

    [Fact]
    public void FromDocument_ShouldRejectMemberMatchingPersistedPackageDefinitionExactly()
    {
        var source = mapper.ToDocument(CreatePackage([Reference(AssetType.Agent, "agent")]))
            .Should().BeOfType<PackageAssetDocument>().Subject;
        var persistedVersion = new AssetVersion("2.0.0");
        var exactSelfMember = new AIAssetReferenceDocument
        {
            AssetType = AIAssetDocumentType.Package,
            AssetId = source.Identity.Id,
            Urn = source.Identity.Urn,
            Version = persistedVersion.Value
        };
        var document = new PackageAssetDocument(
            source.SchemaVersion,
            source.Identity with { Version = persistedVersion.Value },
            source.Metadata,
            source.Lifecycle,
            members: [exactSelfMember],
            references: [],
            dependencies: []);

        var action = () => mapper.FromDocument(document);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot include itself*");
    }

    private static PackageAsset CreatePackage(
        IReadOnlyList<AssetReference> members,
        IReadOnlyList<AssetDependency>? dependencies = null)
        => new PackageAssetFactory().Create(
            new PackageAssetOptions
            {
                Name = "Portable Intelligence",
                Description = "Package persistence fixture.",
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
}
