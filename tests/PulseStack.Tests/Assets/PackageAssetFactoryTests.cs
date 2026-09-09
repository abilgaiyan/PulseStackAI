using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Core.Assets;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Xunit;

namespace PulseStack.Tests.Assets;

public sealed class PackageAssetFactoryTests
{
    [Fact]
    public void Create_ShouldCreatePackage_WithCanonicalDirectMemberProjection()
    {
        var workflow = Reference(AssetType.Workflow, "workflow");
        var library = Reference(AssetType.Library, "library");
        var nestedPackage = Reference(AssetType.Package, "nested");

        var package = Create([workflow, library, nestedPackage]);

        package.Type.Should().Be(AssetType.Package);
        package.Options.Members.Should().Equal(workflow, library, nestedPackage);
        package.References.Should().Equal(workflow, library, nestedPackage);
        package.Dependencies.Should().BeEmpty();
        package.Version.Should().Be(AssetVersion.Initial);
        package.Lifecycle.Should().Be(AssetLifecycle.Draft);
        package.Urn.Value.Should().StartWith("urn:pulsestack:package:");
    }

    [Fact]
    public void Create_ShouldRejectPackageWithoutMembers()
    {
        var action = () => Create([]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one distribution member*");
    }

    [Fact]
    public void Create_ShouldPreserveAuthoredMemberOrder_AndSnapshotMembers()
    {
        var prompt = Reference(AssetType.Prompt, "prompt");
        var workflow = Reference(AssetType.Workflow, "workflow");
        var agent = Reference(AssetType.Agent, "agent");
        var members = new List<AssetReference> { prompt, workflow, agent };

        var package = Create(members);
        members.Clear();

        package.Options.Members.Should().Equal(prompt, workflow, agent);
        package.References.Should().Equal(prompt, workflow, agent);
    }

    [Fact]
    public void Create_ShouldPreserveNestedCompositesAsDirectMembers()
    {
        var library = Reference(AssetType.Library, "library");
        var nestedPackage = Reference(AssetType.Package, "nested");

        var package = Create([library, nestedPackage]);

        package.Options.Members.Should().Equal(library, nestedPackage);
        package.References.Should().Equal(library, nestedPackage);
    }

    [Theory]
    [InlineData(AssetType.Project)]
    [InlineData(AssetType.Library)]
    [InlineData(AssetType.Package)]
    [InlineData(AssetType.Workflow)]
    [InlineData(AssetType.Agent)]
    [InlineData(AssetType.Prompt)]
    [InlineData(AssetType.Tool)]
    [InlineData(AssetType.Knowledge)]
    [InlineData(AssetType.Memory)]
    [InlineData(AssetType.Policy)]
    [InlineData(AssetType.Provider)]
    [InlineData(AssetType.Model)]
    public void Create_ShouldAllowFrozenPackageMemberTypes(AssetType type)
    {
        var member = Reference(type, "member");

        var package = Create([member]);

        package.Options.Members.Should().ContainSingle().Which.Should().Be(member);
        package.References.Should().ContainSingle().Which.Should().Be(member);
    }

    [Fact]
    public void Create_ShouldRejectUnknownPackageMemberType()
    {
        var unknown = Reference((AssetType)int.MaxValue, "unknown");

        var action = () => Create([unknown]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*unknown Asset type*");
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

        var package = Create([first, second]);

        package.Options.Members.Should().Equal(first, second);
        package.References.Should().Equal(first, second);
    }

    [Fact]
    public void Create_ShouldPreserveDependencyOrder_SnapshotDependencies_AndExcludeThemFromReferences()
    {
        var member = Reference(AssetType.Agent, "member");
        var first = new AssetDependency(Reference(AssetType.Knowledge, "first"));
        var second = new AssetDependency(Reference(AssetType.Provider, "second"), false);
        var dependencies = new List<AssetDependency> { first, second };

        var package = Create([member], dependencies);
        dependencies.Clear();

        package.Dependencies.Should().Equal(first, second);
        package.References.Should().Equal(member);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_ShouldRejectMemberAlsoDeclaredAsDependency(bool required)
    {
        var agent = Reference(AssetType.Agent, "agent");

        var action = () => Create([agent], [new AssetDependency(agent, required)]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*members*external dependencies*");
    }

    [Fact]
    public void Create_ShouldRejectMemberDependencyOverlapDespiteDifferentUrn()
    {
        var agent = Reference(AssetType.Agent, "agent");
        var alias = agent with { Urn = new AssetUrn("urn:pulsestack:agent:external") };

        var action = () => Create([agent], [new AssetDependency(alias)]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*member and dependency*conflicting URNs*");
    }

    [Fact]
    public void Create_ShouldRejectDirectSelfMember()
    {
        var id = AssetId.New();
        var self = PackageReference(id);

        var action = () => CreateWithIdentity(id, [self]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot include itself*");
    }

    [Fact]
    public void Create_ShouldRejectDirectSelfDependency()
    {
        var id = AssetId.New();
        var self = PackageReference(id);

        var action = () => CreateWithIdentity(
            id,
            [Reference(AssetType.Agent, "member")],
            [new AssetDependency(self)]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot depend on itself*");
    }

    [Fact]
    public void Create_ShouldAllowSamePackageIdentityAtDifferentVersion()
    {
        var id = AssetId.New();
        var otherVersion = Reference(
            AssetType.Package,
            "other-version",
            id,
            new AssetVersion("2.0.0"));

        var package = CreateWithIdentity(id, [otherVersion]);

        package.Options.Members.Should().ContainSingle().Which.Should().Be(otherVersion);
        package.References.Should().ContainSingle().Which.Should().Be(otherVersion);
    }

    [Fact]
    public void Create_ShouldAllowDependencyWithSamePackageIdentityAtDifferentVersion()
    {
        var id = AssetId.New();
        var otherVersion = Reference(
            AssetType.Package,
            "other-version",
            id,
            new AssetVersion("2.0.0"));

        var package = CreateWithIdentity(
            id,
            [Reference(AssetType.Agent, "member")],
            [new AssetDependency(otherVersion)]);

        package.Dependencies.Should().ContainSingle()
            .Which.Reference.Should().Be(otherVersion);
    }

    [Fact]
    public void Create_ShouldRejectExactDuplicateDependency()
    {
        var dependency = new AssetDependency(Reference(AssetType.Tool, "tool"));

        var action = () => Create(
            [Reference(AssetType.Agent, "member")],
            [dependency, dependency]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dependency*duplicated*");
    }

    [Fact]
    public void Create_ShouldRejectDependencyDefinitionWithConflictingUrn()
    {
        var dependency = Reference(AssetType.Tool, "tool");
        var alias = dependency with { Urn = new AssetUrn("urn:pulsestack:tool:alias") };

        var action = () => Create(
            [Reference(AssetType.Agent, "member")],
            [new AssetDependency(dependency), new AssetDependency(alias)]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dependency*conflicting URNs*");
    }

    [Fact]
    public void Create_ShouldRejectDependencyDefinitionWithConflictingRequiredValue()
    {
        var dependency = Reference(AssetType.Tool, "tool");

        var action = () => Create(
            [Reference(AssetType.Agent, "member")],
            [new AssetDependency(dependency, true), new AssetDependency(dependency, false)]);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*dependency*conflicting Required values*");
    }

    [Fact]
    public void Create_ShouldTreatDifferentVersionsAsDistinctDependencies()
    {
        var id = AssetId.New();
        var first = Reference(AssetType.Tool, "tool", id, new AssetVersion("1.0.0"));
        var second = Reference(AssetType.Tool, "tool-v2", id, new AssetVersion("2.0.0"));

        var package = Create(
            [Reference(AssetType.Agent, "member")],
            [new AssetDependency(first), new AssetDependency(second)]);

        package.Dependencies.Select(dependency => dependency.Reference)
            .Should().Equal(first, second);
    }

    private static PackageAsset Create(
        IReadOnlyList<AssetReference> members,
        IReadOnlyList<AssetDependency>? dependencies = null)
    {
        return new PackageAssetFactory().Create(
            new PackageAssetOptions
            {
                Name = "Portable Intelligence",
                Description = "Package domain conformance fixture.",
                Members = members
            },
            dependencies);
    }

    private static PackageAsset CreateWithIdentity(
        AssetId id,
        IReadOnlyList<AssetReference> members,
        IReadOnlyList<AssetDependency>? dependencies = null)
    {
        var constructor = typeof(PackageAsset)
            .GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types:
                [
                    typeof(AssetId),
                    typeof(AssetUrn),
                    typeof(PackageAssetOptions),
                    typeof(IReadOnlyList<AssetDependency>)
                ],
                modifiers: null)
            ?? throw new InvalidOperationException(
                "PackageAsset domain constructor was not found.");

        try
        {
            return (PackageAsset)constructor.Invoke(
            [
                id,
                new AssetUrn($"urn:pulsestack:package:{id}"),
                new PackageAssetOptions
                {
                    Name = "Portable Intelligence",
                    Description = "Package identity conformance fixture.",
                    Members = members
                },
                dependencies
            ]);
        }
        catch (TargetInvocationException exception)
            when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
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

    private static AssetReference PackageReference(AssetId id)
    {
        return new AssetReference(
            AssetType.Package,
            id,
            new AssetUrn($"urn:pulsestack:package:{id}"),
            AssetVersion.Initial);
    }
}
