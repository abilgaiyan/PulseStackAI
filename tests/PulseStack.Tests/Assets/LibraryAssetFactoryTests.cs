using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;
using Xunit;

namespace PulseStack.Tests.Assets;

public sealed class LibraryAssetFactoryTests
{
    [Fact]
    public void Create_ShouldCreateLibrary_WithCanonicalMemberProjection()
    {
        var workflow = Reference(AssetType.Workflow, "workflow");
        var agent = Reference(AssetType.Agent, "agent");
        var prompt = Reference(AssetType.Prompt, "prompt");

        var library = Create([workflow, agent, prompt]);

        library.Type.Should().Be(AssetType.Library);
        library.Options.Members.Should().Equal(workflow, agent, prompt);
        library.References.Should().Equal(workflow, agent, prompt);
        library.Dependencies.Should().BeEmpty();
        library.Version.Should().Be(AssetVersion.Initial);
        library.Lifecycle.Should().Be(AssetLifecycle.Draft);
        library.Urn.Value.Should().StartWith("urn:pulsestack:library:");
    }

    [Fact]
    public void Create_ShouldAllowEmptyLibrary()
    {
        var library = Create([]);

        library.Options.Members.Should().BeEmpty();
        library.References.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldPreserveAuthoredMemberOrder()
    {
        var prompt = Reference(AssetType.Prompt, "prompt");
        var workflow = Reference(AssetType.Workflow, "workflow");
        var agent = Reference(AssetType.Agent, "agent");

        var library = Create([prompt, workflow, agent]);

        library.Options.Members.Should().Equal(prompt, workflow, agent);
        library.References.Should().Equal(prompt, workflow, agent);
    }

    [Fact]
    public void Create_ShouldSnapshotMembers()
    {
        var workflow = Reference(AssetType.Workflow, "workflow");
        var agent = Reference(AssetType.Agent, "agent");
        var members = new List<AssetReference> { workflow, agent };

        var library = Create(members);
        members.Clear();

        library.Options.Members.Should().Equal(workflow, agent);
        library.References.Should().Equal(workflow, agent);
    }

    [Fact]
    public void Create_ShouldSnapshotDependencies_AndExcludeThemFromReferences()
    {
        var member = Reference(AssetType.Agent, "member");
        var external = Reference(AssetType.Knowledge, "external");
        var dependency = new AssetDependency(external);
        var dependencies = new List<AssetDependency> { dependency };

        var library = Create([member], dependencies);
        dependencies.Clear();

        library.Dependencies.Should().ContainSingle().Which.Should().Be(dependency);
        library.References.Should().Equal(member);
    }

    [Fact]
    public void Create_ShouldAllowExternalLibraryDependency()
    {
        var member = Reference(AssetType.Agent, "member");
        var externalLibrary = Reference(AssetType.Library, "external-library");

        var library = Create(
            [member],
            [new AssetDependency(externalLibrary)]);

        library.Dependencies.Should().ContainSingle();
        library.Dependencies.Single().Reference.Should().Be(externalLibrary);
        library.References.Should().Equal(member);
    }

    [Theory]
    [InlineData(AssetType.Workflow)]
    [InlineData(AssetType.Agent)]
    [InlineData(AssetType.Prompt)]
    [InlineData(AssetType.Tool)]
    [InlineData(AssetType.Knowledge)]
    [InlineData(AssetType.Memory)]
    [InlineData(AssetType.Policy)]
    [InlineData(AssetType.Model)]
    public void Create_ShouldAllowFrozenLibraryMemberTypes(AssetType type)
    {
        var member = Reference(type, "member");

        var library = Create([member]);

        library.Options.Members.Should().ContainSingle().Which.Should().Be(member);
        library.References.Should().ContainSingle().Which.Should().Be(member);
    }

    [Theory]
    [InlineData(AssetType.Project)]
    [InlineData(AssetType.Library)]
    [InlineData(AssetType.Package)]
    [InlineData(AssetType.Provider)]
    public void Create_ShouldRejectProhibitedLibraryMemberTypes(AssetType type)
    {
        var prohibited = Reference(type, "prohibited");

        var action = () => Create([prohibited]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*cannot own*'{type}'*");
    }

    [Fact]
    public void Create_ShouldRejectExactDuplicateMemberDefinition()
    {
        var agent = Reference(AssetType.Agent, "agent");

        var action = () => Create([agent, agent]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicated*");
    }

    [Fact]
    public void Create_ShouldRejectMemberDefinitionWithConflictingUrn()
    {
        var agent = Reference(AssetType.Agent, "agent");
        var conflict = agent with
        {
            Urn = new AssetUrn("urn:pulsestack:agent:conflict")
        };

        var action = () => Create([agent, conflict]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*conflicting URNs*");
    }

    [Fact]
    public void Create_ShouldTreatDifferentVersionsAsDistinctMembers()
    {
        var id = AssetId.New();
        var first = Reference(AssetType.Agent, "agent", id, new AssetVersion("1.0.0"));
        var second = Reference(AssetType.Agent, "agent-v2", id, new AssetVersion("2.0.0"));

        var library = Create([first, second]);

        library.Options.Members.Should().Equal(first, second);
        library.References.Should().Equal(first, second);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_ShouldRejectMemberAlsoDeclaredAsDependency(bool required)
    {
        var agent = Reference(AssetType.Agent, "agent");
        var dependency = new AssetDependency(agent, required);

        var action = () => Create([agent], [dependency]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*members*external dependencies*");
    }

    [Fact]
    public void Create_ShouldRejectMemberDependencyOverlapDespiteDifferentUrn()
    {
        var agent = Reference(AssetType.Agent, "agent");
        var externalAlias = agent with
        {
            Urn = new AssetUrn("urn:pulsestack:agent:external-alias")
        };

        var action = () => Create(
            [agent],
            [new AssetDependency(externalAlias)]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*members*external dependencies*");
    }

    private static LibraryAsset Create(
        IReadOnlyCollection<AssetReference> members,
        IReadOnlyCollection<AssetDependency>? dependencies = null)
    {
        return new LibraryAssetFactory().Create(
            new LibraryAssetOptions
            {
                Name = "Reusable Intelligence",
                Description = "Library domain conformance fixture.",
                Members = members
            },
            dependencies);
    }

    private static AssetReference Reference(
        AssetType type,
        string name,
        AssetId? id = null,
        AssetVersion? version = null)
    {
        var assetId = id ?? AssetId.New();
        return new AssetReference(
            type,
            assetId,
            new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{name}:{assetId}"),
            version ?? AssetVersion.Initial);
    }
}
