using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Core.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphResultBuilderTests
{
    [Fact]
    public void Build_ShouldNormalizeNodesByFrozenDefinitionKeyOrder()
    {
        var workflow = Workflow(Key(AssetType.Workflow, 6));
        var project = Project(Key(AssetType.Project, 9), Reference(workflow), new[] { Reference(workflow) });
        var library = Library(Key(AssetType.Library, 8), Array.Empty<AssetReference>());
        var nestedPackage = Package(Key(AssetType.Package, 7), Array.Empty<AssetReference>());
        var agent = Agent(Key(AssetType.Agent, 5));
        var prompt = Foundation(Key(AssetType.Prompt, 4));
        var tool = Foundation(Key(AssetType.Tool, 3));
        var knowledge = Foundation(Key(AssetType.Knowledge, 2));
        var memory = Foundation(Key(AssetType.Memory, 1));
        var policy = Foundation(Key(AssetType.Policy, 10));
        var model = Foundation(Key(AssetType.Model, 11));
        var descendants = new IAsset[]
        {
            project, library, nestedPackage, workflow, agent, prompt, tool, knowledge, memory, policy, model
        };
        var root = Package(Key(AssetType.Package, 20), descendants.Select(Reference).ToArray());
        var nodes = descendants.Append(root).Select(Node).ToArray();
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        var graph = builder.Build(nodes.Reverse(), RelationshipsFor(nodes).Reverse());

        graph.Nodes.Select(node => node.DefinitionKey.Type).Should().Equal(
            AssetType.Project,
            AssetType.Library,
            AssetType.Package,
            AssetType.Package,
            AssetType.Workflow,
            AssetType.Agent,
            AssetType.Prompt,
            AssetType.Tool,
            AssetType.Knowledge,
            AssetType.Memory,
            AssetType.Policy,
            AssetType.Model);
    }

    [Fact]
    public void Build_ShouldUseGuidThenVersionWithinOneType()
    {
        var lowIdLowVersion = Foundation(Key(AssetType.Tool, 2, AssetVersion.Initial));
        var lowIdHighVersion = Foundation(Key(AssetType.Tool, 2, new AssetVersion("2.0")));
        var highId = Foundation(Key(AssetType.Tool, 9, AssetVersion.Initial));
        var root = Package(
            Key(AssetType.Package, 1),
            new[] { Reference(highId), Reference(lowIdHighVersion), Reference(lowIdLowVersion) });
        var nodes = new[] { Node(highId), Node(lowIdHighVersion), Node(root), Node(lowIdLowVersion) };
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        var graph = builder.Build(nodes, RelationshipsFor(nodes));

        graph.Nodes.Skip(1).Select(node => node.DefinitionKey).Should().Equal(
            AssetDefinitionKey.From(lowIdLowVersion),
            AssetDefinitionKey.From(lowIdHighVersion),
            AssetDefinitionKey.From(highId));
    }

    [Fact]
    public void Build_ShouldNormalizeRelationshipsBySourceThenLocalOrdinal()
    {
        var tool = Foundation(Key(AssetType.Tool, 3));
        var workflow = Workflow(Key(AssetType.Workflow, 2)) with
        {
            Dependencies = new[] { new AssetDependency(Reference(tool), true) }
        };
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(tool), Reference(workflow) });
        var nodes = new[] { Node(tool), Node(workflow), Node(root) };
        var relationships = RelationshipsFor(nodes).ToArray();
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        var graph = builder.Build(nodes.Reverse(), relationships.Reverse());

        graph.Relationships.Select(Signature).Should().BeInAscendingOrder();
        graph.Relationships.Where(edge => edge.SourceKey == AssetDefinitionKey.From(root))
            .Select(edge => edge.LocalOrdinal).Should().Equal(0, 1);
    }

    [Fact]
    public void Build_ShouldPreserveEveryDistinctAuthoredRelationshipOccurrence()
    {
        var tool = Foundation(Key(AssetType.Tool, 4));
        var left = Foundation(Key(AssetType.Prompt, 2)) with
        {
            Dependencies = new[] { new AssetDependency(Reference(tool), true) }
        };
        var right = Foundation(Key(AssetType.Knowledge, 3)) with
        {
            Dependencies = new[] { new AssetDependency(Reference(tool), true) }
        };
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(left), Reference(right) });
        var nodes = new[] { Node(root), Node(left), Node(right), Node(tool) };
        var relationships = RelationshipsFor(nodes).ToArray();
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        var graph = builder.Build(nodes, relationships.Reverse());

        graph.Relationships.Should().HaveCount(4);
        graph.Relationships.Count(edge => AssetDefinitionKey.From(edge.TargetReference) == AssetDefinitionKey.From(tool))
            .Should().Be(2);
    }

    [Fact]
    public void Build_ShouldAllowExcludedOptionalTargetToBeAbsent()
    {
        var absent = Foundation(Key(AssetType.Tool, 2));
        var root = Package(
            Key(AssetType.Package, 1),
            Array.Empty<AssetReference>(),
            new[] { new AssetDependency(Reference(absent), false) });
        var nodes = new[] { Node(root) };
        var relationships = RelationshipsFor(nodes).ToArray();
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        var graph = builder.Build(nodes, relationships);

        graph.Relationships.Should().ContainSingle();
        graph.Relationships[0].MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Excluded);
        graph.Nodes.Should().ContainSingle();
    }

    [Fact]
    public void Build_ShouldPreserveExcludedOptionalWhenTargetMaterializedElsewhere()
    {
        var tool = Foundation(Key(AssetType.Tool, 3));
        var optionalSource = Foundation(Key(AssetType.Prompt, 2)) with
        {
            Dependencies = new[] { new AssetDependency(Reference(tool), false) }
        };
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(optionalSource), Reference(tool) });
        var nodes = new[] { Node(root), Node(optionalSource), Node(tool) };
        var relationships = RelationshipsFor(nodes).ToArray();
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        var graph = builder.Build(nodes, relationships);

        graph.Relationships.Should().Contain(edge =>
            edge.SourceKey == AssetDefinitionKey.From(optionalSource)
            && AssetDefinitionKey.From(edge.TargetReference) == AssetDefinitionKey.From(tool)
            && edge.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Excluded);
    }

    [Fact]
    public void Build_ShouldRejectMissingRequiredTarget()
    {
        var missing = Foundation(Key(AssetType.Tool, 2));
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(missing) });
        var rootNode = Node(root);
        var relationship = RelationshipsFor(new[] { rootNode }).Single();
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        Action act = () => builder.Build(new[] { rootNode }, new[] { relationship });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*required*target*materialized*");
    }

    [Fact]
    public void Build_ShouldRejectMaterializedTargetUrnMismatch()
    {
        var tool = Foundation(Key(AssetType.Tool, 2));
        var wrongReference = new AssetReference(tool.Type, tool.Id, new AssetUrn("urn:pulsestack:test:b6:wrong"), tool.Version);
        var root = Package(Key(AssetType.Package, 1), new[] { wrongReference });
        var nodes = new[] { Node(root), Node(tool) };
        var relationship = RelationshipsFor(new[] { Node(root) }).Single();
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        Action act = () => builder.Build(nodes, new[] { relationship });

        act.Should().Throw<InvalidOperationException>().WithMessage("*URN*");
    }

    [Fact]
    public void Build_ShouldRejectDuplicateDefinitionKeys()
    {
        var root = Package(Key(AssetType.Package, 1), Array.Empty<AssetReference>());
        var rootNode = Node(root);
        var duplicate = new AIAssetGraphNode(rootNode.DefinitionKey, rootNode.Asset);
        var builder = new AIAssetGraphResultBuilder(rootNode.DefinitionKey);

        Action act = () => builder.Build(new[] { rootNode, duplicate }, Array.Empty<AIAssetGraphRelationship>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*more than one node*");
    }

    [Fact]
    public void Build_ShouldRequireExactRootExactlyOnce()
    {
        var root = Package(Key(AssetType.Package, 1), Array.Empty<AssetReference>());
        var other = Foundation(Key(AssetType.Tool, 2));
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        Action act = () => builder.Build(new[] { Node(other) }, Array.Empty<AIAssetGraphRelationship>());

        act.Should().Throw<InvalidOperationException>().WithMessage("*root exactly once*");
    }

    [Fact]
    public void Build_ShouldRejectMissingAuthoredRelationshipOccurrence()
    {
        var tool = Foundation(Key(AssetType.Tool, 2));
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(tool) });
        var nodes = new[] { Node(root), Node(tool) };
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        Action act = () => builder.Build(nodes, Array.Empty<AIAssetGraphRelationship>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*every authoritative relationship authored*");
    }

    [Fact]
    public void Build_ShouldRejectDisconnectedMaterializedNodeOutsideRequiredClosure()
    {
        var root = Package(Key(AssetType.Package, 1), Array.Empty<AssetReference>());
        var unrelated = Foundation(Key(AssetType.Tool, 2));
        var nodes = new[] { Node(root), Node(unrelated) };
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        Action act = () => builder.Build(nodes, Array.Empty<AIAssetGraphRelationship>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the root required-materialization closure*");
    }

    [Fact]
    public void Build_ShouldDetachCollectionSnapshotsFromCallerArrays()
    {
        var tool = Foundation(Key(AssetType.Tool, 2));
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(tool) });
        var nodes = new[] { Node(tool), Node(root) };
        var relationships = RelationshipsFor(nodes).ToArray();
        var originalRelationship = relationships[0];
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        var graph = builder.Build(nodes, relationships);
        nodes[0] = Node(Foundation(Key(AssetType.Model, 9)));
        relationships[0] = new AIAssetGraphRelationship(
            AssetDefinitionKey.From(root),
            Reference(tool),
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphMaterializationAuthority.Excluded,
            AIAssetGraphBoundaryRole.External,
            false,
            0,
            "$.dependencies[0]");

        graph.Nodes.Select(node => node.DefinitionKey).Should().Contain(AssetDefinitionKey.From(tool));
        graph.Relationships.Should().ContainSingle().Which.Should().BeSameAs(originalRelationship);
    }

    [Fact]
    public void Build_ShouldBeIndependentOfInputAndHashIterationOrder()
    {
        var tool = Foundation(Key(AssetType.Tool, 3));
        var workflow = Workflow(Key(AssetType.Workflow, 2)) with
        {
            Dependencies = new[] { new AssetDependency(Reference(tool), true) }
        };
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(workflow), Reference(tool) });
        var nodes = new[] { Node(root), Node(workflow), Node(tool) };
        var relationships = RelationshipsFor(nodes).ToArray();
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        var forward = builder.Build(nodes, relationships);
        var reordered = builder.Build(
            new HashSet<AIAssetGraphNode>(nodes.Reverse()),
            new HashSet<AIAssetGraphRelationship>(relationships.Reverse()));

        reordered.Nodes.Select(node => node.DefinitionKey)
            .Should().Equal(forward.Nodes.Select(node => node.DefinitionKey));
        reordered.Relationships.Select(Signature)
            .Should().Equal(forward.Relationships.Select(Signature));
    }

    private static IEnumerable<AIAssetGraphRelationship> RelationshipsFor(
        IEnumerable<AIAssetGraphNode> nodes)
    {
        var enumerator = new AIAssetGraphRelationshipEnumerator();
        return nodes.SelectMany(node => enumerator.Enumerate(node.Asset)).ToArray();
    }

    private static string Signature(AIAssetGraphRelationship relationship) =>
        $"{relationship.SourceKey.Type}|{relationship.SourceKey.Id.Value:D}|{relationship.SourceKey.Version.Value}|{relationship.LocalOrdinal:D8}|{relationship.AuthoredPath}";

    private static AIAssetGraphNode Node(IAsset asset) =>
        new(AssetDefinitionKey.From(asset), asset);

    private static AssetDefinitionKey Key(
        AssetType type,
        int value,
        AssetVersion? version = null) =>
        new(
            type,
            new AssetId(Guid.Parse($"00000000-0000-0000-0000-{value:D12}")),
            version ?? AssetVersion.Initial);

    private static TestAsset Foundation(AssetDefinitionKey key) =>
        new(key, new AssetUrn($"urn:pulsestack:test:b6:{key.Type}:{key.Id.Value:D}:{key.Version.Value}"));

    private static PackageAsset Package(
        AssetDefinitionKey key,
        IReadOnlyList<AssetReference> members,
        IReadOnlyList<AssetDependency>? dependencies = null) =>
        Construct<PackageAsset>(
            key.Id,
            new AssetUrn($"urn:pulsestack:test:b6:package:{key.Id.Value:D}"),
            key.Version,
            new PackageAssetOptions { Name = "package", Description = "package", Members = members },
            dependencies ?? Array.Empty<AssetDependency>());

    private static LibraryAsset Library(
        AssetDefinitionKey key,
        IReadOnlyList<AssetReference> members) =>
        Construct<LibraryAsset>(
            key.Id,
            new AssetUrn($"urn:pulsestack:test:b6:library:{key.Id.Value:D}"),
            new LibraryAssetOptions { Name = "library", Description = "library", Members = members },
            Array.Empty<AssetDependency>());

    private static ProjectAsset Project(
        AssetDefinitionKey key,
        AssetReference entryWorkflow,
        IReadOnlyList<AssetReference> ownedAssets) =>
        Construct<ProjectAsset>(
            key.Id,
            new AssetUrn($"urn:pulsestack:test:b6:project:{key.Id.Value:D}"),
            new ProjectAssetOptions
            {
                Name = "project",
                EntryWorkflow = entryWorkflow,
                OwnedAssets = ownedAssets
            },
            Array.Empty<AssetDependency>());

    private static WorkflowAsset Workflow(AssetDefinitionKey key) =>
        Construct<WorkflowAsset>(
            key.Id,
            new AssetUrn($"urn:pulsestack:test:b6:workflow:{key.Id.Value:D}"),
            new WorkflowAssetOptions { Name = "workflow", Steps = Array.Empty<WorkflowStepDefinition>() });

    private static AgentDefinition Agent(AssetDefinitionKey key) =>
        Construct<AgentDefinition>(
            key.Id,
            new AssetUrn($"urn:pulsestack:test:b6:agent:{key.Id.Value:D}"),
            new AgentDefinitionOptions
            {
                Name = "agent",
                Goal = "goal",
                Role = "role"
            });

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static T Construct<T>(params object?[] arguments) where T : class
    {
        var constructor = typeof(T)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(candidate => candidate.GetParameters().Length == arguments.Length)
            .First(candidate => ParametersMatch(candidate.GetParameters(), arguments));
        return (T)constructor.Invoke(arguments);
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, object?[] arguments)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (arguments[index] is not null
                && !parameters[index].ParameterType.IsInstanceOfType(arguments[index]))
            {
                return false;
            }
        }

        return true;
    }

    private sealed record TestAsset : Asset
    {
        [SetsRequiredMembers]
        internal TestAsset(AssetDefinitionKey key, AssetUrn urn)
            : base(key.Type)
        {
            Id = key.Id;
            Urn = urn;
            Version = key.Version;
            Metadata = new AssetMetadata { Name = "test" };
            Lifecycle = AssetLifecycle.Published;
        }
    }
}
