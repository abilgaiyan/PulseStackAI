using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;
using Xunit;

namespace PulseStack.Tests.Assets;

public sealed class ProjectAssetFactoryTests
{
    [Fact]
    public void Create_ShouldCreateProject_WithEntryFirstCanonicalProjection()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");
        var prompt = Reference(AssetType.Prompt, "prompt");

        var project = Create(entry, [agent, entry, prompt]);

        project.Type.Should().Be(AssetType.Project);
        project.Options.EntryWorkflow.Should().Be(entry);
        project.Options.OwnedAssets.Should().Equal(agent, entry, prompt);
        project.References.Should().Equal(entry, agent, prompt);
        project.Dependencies.Should().BeEmpty();
        project.Urn.Value.Should().StartWith("urn:pulsestack:project:");
    }

    [Fact]
    public void Create_ShouldSnapshotOwnedAssets()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");
        var owned = new List<AssetReference> { entry, agent };

        var project = Create(entry, owned);
        owned.Clear();

        project.Options.OwnedAssets.Should().Equal(entry, agent);
        project.References.Should().Equal(entry, agent);
    }

    [Fact]
    public void Create_ShouldSnapshotDependencies_AndExcludeThemFromReferences()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var external = Reference(AssetType.Knowledge, "external");
        var dependency = new AssetDependency(external);
        var dependencies = new List<AssetDependency> { dependency };

        var project = Create(entry, [entry], dependencies);
        dependencies.Clear();

        project.Dependencies.Should().ContainSingle().Which.Should().Be(dependency);
        project.References.Should().Equal(entry);
    }

    [Fact]
    public void Create_ShouldRejectNonWorkflowEntry()
    {
        var entry = Reference(AssetType.Agent, "entry");

        var action = () => Create(entry, [entry]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*entry workflow*Workflow Asset*");
    }

    [Theory]
    [InlineData(AssetType.Project)]
    [InlineData(AssetType.Library)]
    [InlineData(AssetType.Package)]
    [InlineData(AssetType.Provider)]
    public void Create_ShouldRejectProhibitedOwnedAssetTypes(AssetType type)
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var prohibited = Reference(type, "prohibited");

        var action = () => Create(entry, [entry, prohibited]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage($"*cannot own*'{type}'*");
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
    public void Create_ShouldAllowFrozenOwnedAssetTypes(AssetType type)
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var owned = type == AssetType.Workflow
            ? Reference(AssetType.Workflow, "secondary")
            : Reference(type, "owned");

        var project = Create(entry, [entry, owned]);

        project.Options.OwnedAssets.Should().Equal(entry, owned);
    }

    [Fact]
    public void Create_ShouldRejectExactDuplicateOwnedDefinition()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");

        var action = () => Create(entry, [entry, agent, agent]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicated*");
    }

    [Fact]
    public void Create_ShouldRejectOwnedDefinitionWithConflictingUrn()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");
        var conflict = agent with { Urn = new AssetUrn("urn:pulsestack:agent:conflict") };

        var action = () => Create(entry, [entry, agent, conflict]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*conflicting URNs*");
    }

    [Fact]
    public void Create_ShouldRejectEntryWhenDefinitionKeyIsOwnedWithDifferentUrn()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var conflictingEntry = entry with
        {
            Urn = new AssetUrn("urn:pulsestack:workflow:conflict")
        };

        var action = () => Create(entry, [conflictingEntry]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*exactly included in OwnedAssets*");
    }

    [Fact]
    public void Create_ShouldRejectMissingEntryOwnership()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");

        var action = () => Create(entry, [agent]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*exactly included in OwnedAssets*");
    }

    [Fact]
    public void Create_ShouldRejectProjectDependency()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var dependency = new AssetDependency(Reference(AssetType.Project, "other-project"));

        var action = () => Create(entry, [entry], [dependency]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot depend on another Project Asset*");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_ShouldRejectOwnedAssetAlsoDeclaredAsDependency(bool required)
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");
        var dependency = new AssetDependency(agent, required);

        var action = () => Create(entry, [entry, agent], [dependency]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*owned Assets*external dependencies*");
    }

    [Fact]
    public void Create_ShouldRejectOwnedDependencyOverlapDespiteDifferentUrn()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var agent = Reference(AssetType.Agent, "agent");
        var externalAlias = agent with
        {
            Urn = new AssetUrn("urn:pulsestack:agent:external-alias")
        };

        var action = () => Create(
            entry,
            [entry, agent],
            [new AssetDependency(externalAlias)]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*owned Assets*external dependencies*");
    }

    private static ProjectAsset Create(
        AssetReference entry,
        IReadOnlyList<AssetReference> owned,
        IReadOnlyCollection<AssetDependency>? dependencies = null)
    {
        return new ProjectAssetFactory().Create(
            new ProjectAssetOptions
            {
                Name = "Factory Intelligence",
                Description = "Project domain conformance fixture.",
                EntryWorkflow = entry,
                OwnedAssets = owned
            },
            dependencies);
    }

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
