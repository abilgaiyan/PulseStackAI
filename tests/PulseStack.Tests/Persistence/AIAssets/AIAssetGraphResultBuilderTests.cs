using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Core.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphResultBuilderTests
{
    [Fact]
    public async Task CompletedSuccessfulExpansion_ShouldMintSnapshotAndBuildGraph()
    {
        var tool = Foundation(Key(AssetType.Tool, 2));
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(tool) });
        var completion = await AIAssetGraphSuccessfulOperationSnapshot.CompleteAsync(
            new ScriptedResolver(root, tool),
            AssetDefinitionKey.From(root));

        completion.Failure.Should().BeNull();
        completion.Success.Should().NotBeNull();

        var graph = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root))
            .Build(completion.Success!);

        graph.Nodes.Select(static node => node.DefinitionKey).Should().Equal(
            AssetDefinitionKey.From(root),
            AssetDefinitionKey.From(tool));
        graph.Relationships.Should().ContainSingle();
    }

    [Fact]
    public async Task RequiredCycle_ShouldReturnFailureWithoutSuccessSnapshot()
    {
        var a = Foundation(Key(AssetType.Tool, 2));
        var b = Foundation(Key(AssetType.Prompt, 3));
        a = a with { Dependencies = new[] { new AssetDependency(Reference(b), true) } };
        b = b with { Dependencies = new[] { new AssetDependency(Reference(a), true) } };
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(a) });

        var completion = await AIAssetGraphSuccessfulOperationSnapshot.CompleteAsync(
            new ScriptedResolver(root, a, b),
            AssetDefinitionKey.From(root));

        completion.Failure.Should().BeOfType<AIAssetGraphLoadResult.RequiredMaterializationCycle>();
        completion.Success.Should().BeNull();
    }

    [Fact]
    public void Build_ShouldNormalizeNodesByTypeGuidAndVersion()
    {
        var lowV1 = Foundation(Key(AssetType.Tool, 2, AssetVersion.Initial));
        var lowV2 = Foundation(Key(AssetType.Tool, 2, new AssetVersion("2.0")));
        var high = Foundation(Key(AssetType.Tool, 9));
        var prompt = Foundation(Key(AssetType.Prompt, 4));
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(high), Reference(lowV2), Reference(lowV1), Reference(prompt) });
        var nodes = new[] { Node(high), Node(lowV2), Node(root), Node(prompt), Node(lowV1) };
        var snapshot = ForgeSnapshot(AssetDefinitionKey.From(root), nodes.Reverse(), RelationshipsFor(nodes));

        var graph = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root)).Build(snapshot);

        graph.Nodes.Select(static node => node.DefinitionKey).Should().Equal(
            AssetDefinitionKey.From(root),
            AssetDefinitionKey.From(prompt),
            AssetDefinitionKey.From(lowV1),
            AssetDefinitionKey.From(lowV2),
            AssetDefinitionKey.From(high));
    }

    [Fact]
    public void Build_ShouldNormalizeRelationshipsBySourceThenLocalOrdinal()
    {
        var left = Foundation(Key(AssetType.Tool, 2));
        var right = Foundation(Key(AssetType.Prompt, 3));
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(right), Reference(left) });
        var nodes = new[] { Node(left), Node(root), Node(right) };
        var relationships = RelationshipsFor(nodes).Reverse().ToArray();
        var snapshot = ForgeSnapshot(AssetDefinitionKey.From(root), nodes, relationships);

        var graph = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root)).Build(snapshot);

        graph.Relationships.Select(static relationship => relationship.LocalOrdinal).Should().Equal(0, 1);
    }

    [Fact]
    public void Build_ShouldPreserveOptionalAbsentAndPresentTargets()
    {
        var present = Foundation(Key(AssetType.Tool, 2));
        var absent = Foundation(Key(AssetType.Knowledge, 4));
        var optionalSource = Foundation(Key(AssetType.Prompt, 3)) with
        {
            Dependencies = new[]
            {
                new AssetDependency(Reference(present), false),
                new AssetDependency(Reference(absent), false)
            }
        };
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(optionalSource), Reference(present) });
        var nodes = new[] { Node(root), Node(optionalSource), Node(present) };
        var snapshot = ForgeSnapshot(AssetDefinitionKey.From(root), nodes, RelationshipsFor(nodes));

        var graph = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root)).Build(snapshot);

        graph.Relationships.Should().Contain(edge =>
            AssetDefinitionKey.From(edge.TargetReference) == AssetDefinitionKey.From(present)
            && edge.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Excluded);
        graph.Relationships.Should().Contain(edge =>
            AssetDefinitionKey.From(edge.TargetReference) == AssetDefinitionKey.From(absent)
            && edge.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Excluded);
        graph.Nodes.Should().NotContain(node => node.DefinitionKey == AssetDefinitionKey.From(absent));
    }

    [Fact]
    public void Build_ShouldRejectMissingAuthoredRelationshipOccurrence()
    {
        var tool = Foundation(Key(AssetType.Tool, 2));
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(tool) });
        var snapshot = ForgeSnapshot(
            AssetDefinitionKey.From(root),
            new[] { Node(root), Node(tool) },
            Array.Empty<AIAssetGraphRelationship>());

        Action act = () => new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root)).Build(snapshot);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*every authoritative relationship authored*");
    }

    [Fact]
    public void Build_ShouldRejectDisconnectedMaterializedNode()
    {
        var member = Foundation(Key(AssetType.Prompt, 3));
        var unrelated = Foundation(Key(AssetType.Tool, 2));
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(member) });
        var nodes = new[] { Node(root), Node(member), Node(unrelated) };
        var snapshot = ForgeSnapshot(AssetDefinitionKey.From(root), nodes, RelationshipsFor(nodes));

        Action act = () => new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root)).Build(snapshot);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*outside the root required-materialization closure*");
    }

    [Fact]
    public void Build_ShouldRejectMissingRequiredTargetAndUrnMismatch()
    {
        var missing = Foundation(Key(AssetType.Tool, 2));
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(missing) });
        var rootNode = Node(root);
        var missingSnapshot = ForgeSnapshot(
            AssetDefinitionKey.From(root),
            new[] { rootNode },
            RelationshipsFor(new[] { rootNode }));

        Action missingAct = () => new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root)).Build(missingSnapshot);
        missingAct.Should().Throw<InvalidOperationException>().WithMessage("*required*target*materialized*");

        var materialized = Foundation(Key(AssetType.Tool, 2));
        var wrongReference = new AssetReference(
            materialized.Type,
            materialized.Id,
            new AssetUrn("urn:pulsestack:test:b6:wrong"),
            materialized.Version);
        var mismatchRoot = Package(Key(AssetType.Package, 5), new[] { wrongReference });
        var mismatchNodes = new[] { Node(mismatchRoot), Node(materialized) };
        var mismatchSnapshot = ForgeSnapshot(
            AssetDefinitionKey.From(mismatchRoot),
            mismatchNodes,
            RelationshipsFor(new[] { Node(mismatchRoot) }));

        Action mismatchAct = () => new AIAssetGraphResultBuilder(AssetDefinitionKey.From(mismatchRoot)).Build(mismatchSnapshot);
        mismatchAct.Should().Throw<InvalidOperationException>().WithMessage("*URN*");
    }

    [Fact]
    public void Build_ShouldRejectDuplicateNodeAndWrongRoot()
    {
        var member = Foundation(Key(AssetType.Tool, 2));
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(member) });
        var rootNode = Node(root);
        var duplicateSnapshot = ForgeSnapshot(
            AssetDefinitionKey.From(root),
            new[] { rootNode, new AIAssetGraphNode(rootNode.DefinitionKey, rootNode.Asset), Node(member) },
            RelationshipsFor(new[] { rootNode, Node(member) }));

        Action duplicateAct = () => new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root)).Build(duplicateSnapshot);
        duplicateAct.Should().Throw<InvalidOperationException>().WithMessage("*more than one node*");

        var wrongRoot = Package(Key(AssetType.Package, 5), new[] { Reference(member) });
        var validSnapshot = ForgeSnapshot(
            AssetDefinitionKey.From(root),
            new[] { rootNode, Node(member) },
            RelationshipsFor(new[] { rootNode, Node(member) }));

        Action wrongRootAct = () => new AIAssetGraphResultBuilder(AssetDefinitionKey.From(wrongRoot)).Build(validSnapshot);
        wrongRootAct.Should().Throw<InvalidOperationException>().WithMessage("*snapshot root*");
    }

    [Fact]
    public void Build_ShouldDetachAndRemainOrderIndependent()
    {
        var left = Foundation(Key(AssetType.Tool, 2));
        var right = Foundation(Key(AssetType.Prompt, 3));
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(left), Reference(right) });
        var nodes = new[] { Node(root), Node(left), Node(right) };
        var relationships = RelationshipsFor(nodes).ToArray();
        var forwardSnapshot = ForgeSnapshot(AssetDefinitionKey.From(root), nodes, relationships);
        var reversedSnapshot = ForgeSnapshot(
            AssetDefinitionKey.From(root),
            new HashSet<AIAssetGraphNode>(nodes.Reverse()),
            new HashSet<AIAssetGraphRelationship>(relationships.Reverse()));
        var builder = new AIAssetGraphResultBuilder(AssetDefinitionKey.From(root));

        var forward = builder.Build(forwardSnapshot);
        var reversed = builder.Build(reversedSnapshot);
        nodes[0] = Node(Foundation(Key(AssetType.Model, 9)));
        relationships[0] = new AIAssetGraphRelationship(
            AssetDefinitionKey.From(root),
            Reference(left),
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphMaterializationAuthority.Excluded,
            AIAssetGraphBoundaryRole.External,
            false,
            0,
            "$.dependencies[0]");

        reversed.Nodes.Select(static node => node.DefinitionKey)
            .Should().Equal(forward.Nodes.Select(static node => node.DefinitionKey));
        reversed.Relationships.Select(Signature)
            .Should().Equal(forward.Relationships.Select(Signature));
        forward.Nodes.Should().Contain(node => node.DefinitionKey == AssetDefinitionKey.From(root));
    }

    private static AIAssetGraphSuccessfulOperationSnapshot ForgeSnapshot(
        AssetDefinitionKey rootKey,
        IEnumerable<AIAssetGraphNode> nodes,
        IEnumerable<AIAssetGraphRelationship> relationships)
    {
        var constructor = typeof(AIAssetGraphSuccessfulOperationSnapshot)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single();
        return (AIAssetGraphSuccessfulOperationSnapshot)constructor.Invoke(
            new object[] { rootKey, nodes.ToArray(), relationships.ToArray() });
    }

    private static IEnumerable<AIAssetGraphRelationship> RelationshipsFor(IEnumerable<AIAssetGraphNode> nodes)
    {
        var enumerator = new AIAssetGraphRelationshipEnumerator();
        return nodes.SelectMany(node => enumerator.Enumerate(node.Asset)).ToArray();
    }

    private static string Signature(AIAssetGraphRelationship relationship) =>
        $"{relationship.SourceKey.Type}|{relationship.SourceKey.Id.Value:D}|{relationship.SourceKey.Version.Value}|{relationship.LocalOrdinal:D8}|{relationship.AuthoredPath}";

    private static AIAssetGraphNode Node(IAsset asset) => new(AssetDefinitionKey.From(asset), asset);

    private static AssetDefinitionKey Key(AssetType type, int value, AssetVersion? version = null) =>
        new(type, new AssetId(Guid.Parse($"00000000-0000-0000-0000-{value:D12}")), version ?? AssetVersion.Initial);

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

    private sealed class ScriptedResolver : IPersistentAIAssetResolver
    {
        private readonly IReadOnlyDictionary<AssetDefinitionKey, IAsset> assets;

        internal ScriptedResolver(params IAsset[] assets) =>
            this.assets = assets.ToDictionary(AssetDefinitionKey.From);

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<AIAssetResolutionResult>(
                assets.TryGetValue(key, out var asset)
                    ? new AIAssetResolutionResult.Resolved(asset)
                    : new AIAssetResolutionResult.DefinitionNotPublished());
        }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetReference reference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = AssetDefinitionKey.From(reference);
            if (!assets.TryGetValue(key, out var asset))
            {
                return ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.DefinitionNotPublished());
            }

            return ValueTask.FromResult<AIAssetResolutionResult>(
                asset.Urn == reference.Urn
                    ? new AIAssetResolutionResult.Resolved(asset)
                    : new AIAssetResolutionResult.ReferenceMismatch());
        }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetUrn urn,
            AssetVersion version,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var asset = assets.Values.FirstOrDefault(candidate => candidate.Urn == urn && candidate.Version == version);
            return ValueTask.FromResult<AIAssetResolutionResult>(
                asset is null
                    ? new AIAssetResolutionResult.LineageNotPublished()
                    : new AIAssetResolutionResult.Resolved(asset));
        }

        public ValueTask<CatalogLineageLookupResult> DiscoverLineageAsync(
            AssetUrn urn,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<CatalogLineageLookupResult>(new CatalogLineageLookupResult.NotFound());
        }
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
