using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Core.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphResultBuilderTests
{
    [Fact]
    public void Build_ShouldNormalizeNodesByFrozenDefinitionKeyOrder()
    {
        var project = Node(AssetType.Project, 9);
        var library = Node(AssetType.Library, 8);
        var package = Node(AssetType.Package, 7);
        var workflow = Node(AssetType.Workflow, 6);
        var agent = Node(AssetType.Agent, 5);
        var prompt = Node(AssetType.Prompt, 4);
        var tool = Node(AssetType.Tool, 3);
        var knowledge = Node(AssetType.Knowledge, 2);
        var memory = Node(AssetType.Memory, 1);
        var policy = Node(AssetType.Policy, 10);
        var model = Node(AssetType.Model, 11);
        var builder = new AIAssetGraphResultBuilder(project.DefinitionKey);

        var graph = builder.Build(
            new[] { model, tool, package, policy, project, memory, prompt, agent, library, knowledge, workflow },
            Array.Empty<AIAssetGraphRelationship>());

        graph.Nodes.Select(node => node.DefinitionKey.Type).Should().Equal(
            AssetType.Project,
            AssetType.Library,
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
        var root = Node(AssetType.Package, 1);
        var highId = Node(AssetType.Tool, 9, AssetVersion.Initial);
        var lowIdHighVersion = Node(AssetType.Tool, 2, new AssetVersion("2.0"));
        var lowIdLowVersion = Node(AssetType.Tool, 2, AssetVersion.Initial);
        var builder = new AIAssetGraphResultBuilder(root.DefinitionKey);

        var graph = builder.Build(
            new[] { highId, lowIdHighVersion, root, lowIdLowVersion },
            Array.Empty<AIAssetGraphRelationship>());

        graph.Nodes.Skip(1).Select(node => node.DefinitionKey).Should().Equal(
            lowIdLowVersion.DefinitionKey,
            lowIdHighVersion.DefinitionKey,
            highId.DefinitionKey);
    }

    [Fact]
    public void Build_ShouldNormalizeRelationshipsBySourceThenLocalOrdinal()
    {
        var root = Node(AssetType.Package, 1);
        var workflow = Node(AssetType.Workflow, 2);
        var tool = Node(AssetType.Tool, 3);
        var rootLater = Required(root, tool, 2, "Members[2]");
        var workflowFirst = Required(workflow, tool, 0, "Dependencies[0]");
        var rootEarlier = Required(root, workflow, 0, "Members[0]");
        var builder = new AIAssetGraphResultBuilder(root.DefinitionKey);

        var graph = builder.Build(
            new[] { tool, workflow, root },
            new[] { workflowFirst, rootLater, rootEarlier });

        graph.Relationships.Should().Equal(rootEarlier, rootLater, workflowFirst);
    }

    [Fact]
    public void Build_ShouldPreserveDistinctAuthoredRelationshipOccurrences()
    {
        var root = Node(AssetType.Package, 1);
        var tool = Node(AssetType.Tool, 2);
        var first = Required(root, tool, 0, "Members[0]");
        var second = Required(root, tool, 1, "Members[1]");
        var builder = new AIAssetGraphResultBuilder(root.DefinitionKey);

        var graph = builder.Build(new[] { root, tool }, new[] { second, first });

        graph.Relationships.Should().Equal(first, second);
        graph.Relationships.Should().HaveCount(2);
    }

    [Fact]
    public void Build_ShouldAllowExcludedOptionalTargetToBeAbsent()
    {
        var root = Node(AssetType.Package, 1);
        var absent = Node(AssetType.Tool, 2);
        var optional = Optional(root, absent, 0);
        var builder = new AIAssetGraphResultBuilder(root.DefinitionKey);

        var graph = builder.Build(new[] { root }, new[] { optional });

        graph.Relationships.Should().ContainSingle().Which.Should().BeSameAs(optional);
        graph.Nodes.Should().ContainSingle().Which.Should().BeSameAs(root);
    }

    [Fact]
    public void Build_ShouldPreserveExcludedOptionalWhenTargetMaterializedElsewhere()
    {
        var root = Node(AssetType.Package, 1);
        var tool = Node(AssetType.Tool, 2);
        var optional = Optional(root, tool, 0);
        var builder = new AIAssetGraphResultBuilder(root.DefinitionKey);

        var graph = builder.Build(new[] { tool, root }, new[] { optional });

        graph.Relationships.Should().ContainSingle();
        graph.Relationships[0].MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Excluded);
        graph.Nodes.Should().Contain(node => node.DefinitionKey == tool.DefinitionKey);
    }

    [Fact]
    public void Build_ShouldRejectMissingRequiredTarget()
    {
        var root = Node(AssetType.Package, 1);
        var missing = Node(AssetType.Tool, 2);
        var builder = new AIAssetGraphResultBuilder(root.DefinitionKey);

        Action act = () => builder.Build(new[] { root }, new[] { Required(root, missing, 0, "Members[0]") });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*required*target*materialized*");
    }

    [Fact]
    public void Build_ShouldRejectMaterializedTargetUrnMismatch()
    {
        var root = Node(AssetType.Package, 1);
        var tool = Node(AssetType.Tool, 2);
        var relationship = new AIAssetGraphRelationship(
            root.DefinitionKey,
            new AssetReference(tool.DefinitionKey.Type, tool.DefinitionKey.Id, new AssetUrn("urn:pulsestack:test:b6:wrong"), tool.DefinitionKey.Version),
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.External,
            true,
            0,
            "Dependencies[0]");
        var builder = new AIAssetGraphResultBuilder(root.DefinitionKey);

        Action act = () => builder.Build(new[] { root, tool }, new[] { relationship });

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*URN*");
    }

    [Fact]
    public void Build_ShouldRejectDuplicateDefinitionKeys()
    {
        var root = Node(AssetType.Package, 1);
        var duplicate = new AIAssetGraphNode(root.DefinitionKey, root.Asset);
        var builder = new AIAssetGraphResultBuilder(root.DefinitionKey);

        Action act = () => builder.Build(new[] { root, duplicate }, Array.Empty<AIAssetGraphRelationship>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*more than one node*");
    }

    [Fact]
    public void Build_ShouldRequireExactRootExactlyOnce()
    {
        var root = Node(AssetType.Package, 1);
        var other = Node(AssetType.Tool, 2);
        var builder = new AIAssetGraphResultBuilder(root.DefinitionKey);

        Action act = () => builder.Build(new[] { other }, Array.Empty<AIAssetGraphRelationship>());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*root exactly once*");
    }

    [Fact]
    public void Build_ShouldDetachCollectionSnapshotsFromCallerArrays()
    {
        var root = Node(AssetType.Package, 1);
        var tool = Node(AssetType.Tool, 2);
        var relationship = Required(root, tool, 0, "Members[0]");
        var nodes = new[] { tool, root };
        var relationships = new[] { relationship };
        var builder = new AIAssetGraphResultBuilder(root.DefinitionKey);

        var graph = builder.Build(nodes, relationships);
        nodes[0] = Node(AssetType.Model, 9);
        relationships[0] = Optional(root, tool, 0);

        graph.Nodes.Select(node => node.DefinitionKey).Should().Contain(tool.DefinitionKey);
        graph.Relationships.Should().ContainSingle().Which.Should().BeSameAs(relationship);
    }

    [Fact]
    public void Build_ShouldBeIndependentOfInputAndHashIterationOrder()
    {
        var root = Node(AssetType.Package, 1);
        var workflow = Node(AssetType.Workflow, 2);
        var tool = Node(AssetType.Tool, 3);
        var first = Required(root, workflow, 0, "Members[0]");
        var second = Required(root, tool, 1, "Members[1]");
        var third = Required(workflow, tool, 0, "Dependencies[0]");
        var builder = new AIAssetGraphResultBuilder(root.DefinitionKey);

        var forward = builder.Build(
            new[] { root, workflow, tool },
            new[] { first, second, third });
        var reversed = builder.Build(
            new HashSet<AIAssetGraphNode> { tool, root, workflow },
            new HashSet<AIAssetGraphRelationship> { third, second, first });

        reversed.Nodes.Select(node => node.DefinitionKey)
            .Should().Equal(forward.Nodes.Select(node => node.DefinitionKey));
        reversed.Relationships.Select(Signature)
            .Should().Equal(forward.Relationships.Select(Signature));
    }

    private static string Signature(AIAssetGraphRelationship relationship) =>
        $"{relationship.SourceKey.Type}|{relationship.SourceKey.Id.Value:D}|{relationship.SourceKey.Version.Value}|{relationship.LocalOrdinal}|{relationship.AuthoredPath}";

    private static AIAssetGraphRelationship Required(
        AIAssetGraphNode source,
        AIAssetGraphNode target,
        int ordinal,
        string path) =>
        new(
            source.DefinitionKey,
            Reference(target),
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.External,
            true,
            ordinal,
            path);

    private static AIAssetGraphRelationship Optional(
        AIAssetGraphNode source,
        AIAssetGraphNode target,
        int ordinal) =>
        new(
            source.DefinitionKey,
            Reference(target),
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphMaterializationAuthority.Excluded,
            AIAssetGraphBoundaryRole.External,
            false,
            ordinal,
            "Dependencies[0]");

    private static AssetReference Reference(AIAssetGraphNode target) =>
        new(target.DefinitionKey.Type, target.DefinitionKey.Id, target.Asset.Urn, target.DefinitionKey.Version);

    private static AIAssetGraphNode Node(
        AssetType type,
        int id,
        AssetVersion? version = null)
    {
        version ??= AssetVersion.Initial;
        var assetId = new AssetId(Guid.Parse($"00000000-0000-0000-0000-{id:D12}"));
        var asset = new TestAsset(type)
        {
            Id = assetId,
            Urn = new AssetUrn($"urn:pulsestack:test:b6:{type}:{id}:{version.Value}"),
            Version = version,
            Metadata = new AssetMetadata { Name = $"b6-{type}-{id}" }
        };

        return new AIAssetGraphNode(AssetDefinitionKey.From(asset), asset);
    }

    private sealed record TestAsset : Asset
    {
        internal TestAsset(AssetType type)
            : base(type)
        {
        }
    }
}
