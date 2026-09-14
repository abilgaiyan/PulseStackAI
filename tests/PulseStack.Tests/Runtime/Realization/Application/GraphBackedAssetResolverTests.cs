using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Core.Runtime.Realization.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Application;

public sealed class GraphBackedAssetResolverTests
{
    private static readonly AssetType[] SupportedTypes =
    [
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
        AssetType.Model
    ];

    [Fact]
    public void Constructor_ShouldRequireGraph()
    {
        var action = () => new GraphBackedAssetResolver(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnExactGraphResidentAsset()
    {
        var root = Asset(AssetType.Project, "root");
        var workflow = Asset(AssetType.Workflow, "entry");
        var resolver = Resolver(root, workflow);

        var result = await resolver.ResolveAsync(Reference(workflow));

        result.Should().BeSameAs(workflow);
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNullWhenDefinitionKeyIsAbsent()
    {
        var root = Asset(AssetType.Project, "root");
        var outside = Asset(AssetType.Workflow, "outside");
        var resolver = Resolver(root);

        var result = await resolver.ResolveAsync(Reference(outside));

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNullWhenDefinitionKeyMatchesButUrnDiffersOrdinally()
    {
        var root = Asset(AssetType.Project, "root");
        var workflow = Asset(AssetType.Workflow, "entry");
        var resolver = Resolver(root, workflow);
        var reference = new AssetReference(
            workflow.Type,
            workflow.Id,
            new AssetUrn(workflow.Urn.Value.ToUpperInvariant()),
            workflow.Version);

        var result = await resolver.ResolveAsync(reference);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnNullForMalformedReferences()
    {
        var root = Asset(AssetType.Project, "root");
        var workflow = Asset(AssetType.Workflow, "entry");
        var resolver = Resolver(root, workflow);

        var malformed = new[]
        {
            new AssetReference(
                AssetType.Workflow,
                AssetId.Empty,
                workflow.Urn,
                workflow.Version),
            new AssetReference(
                AssetType.Workflow,
                workflow.Id,
                null!,
                workflow.Version),
            new AssetReference(
                AssetType.Workflow,
                workflow.Id,
                new AssetUrn(" "),
                workflow.Version),
            new AssetReference(
                AssetType.Workflow,
                workflow.Id,
                workflow.Urn,
                null!),
            new AssetReference(
                AssetType.Workflow,
                workflow.Id,
                workflow.Urn,
                new AssetVersion(" ")),
            new AssetReference(
                AssetType.Provider,
                workflow.Id,
                workflow.Urn,
                workflow.Version),
            new AssetReference(
                (AssetType)int.MaxValue,
                workflow.Id,
                workflow.Urn,
                workflow.Version)
        };

        foreach (var reference in malformed)
        {
            var result = await resolver.ResolveAsync(reference);
            result.Should().BeNull();
        }
    }

    [Fact]
    public void ResolveAsync_ShouldRejectNullReference()
    {
        var resolver = Resolver(Asset(AssetType.Project, "root"));

        var action = () => resolver.ResolveAsync(null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ResolveAsync_ShouldObserveCancellationBeforeNullReferenceValidation()
    {
        var resolver = Resolver(Asset(AssetType.Project, "root"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => resolver.ResolveAsync(null!, cancellation.Token);

        action.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void ResolveAsync_ShouldObserveCancellationBeforeMalformedReferenceValidation()
    {
        var resolver = Resolver(Asset(AssetType.Project, "root"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var malformed = new AssetReference(
            AssetType.Workflow,
            AssetId.Empty,
            null!,
            null!);

        var action = () => resolver.ResolveAsync(malformed, cancellation.Token);

        action.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void ResolveAsync_ShouldObserveCancellationBeforeValidLookup()
    {
        var root = Asset(AssetType.Project, "root");
        var resolver = Resolver(root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = () => resolver.ResolveAsync(Reference(root), cancellation.Token);

        action.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public async Task ResolveAsync_ShouldBeIsolatedToItsGraphSnapshot()
    {
        var rootA = Asset(AssetType.Project, "root-a");
        var rootB = Asset(AssetType.Project, "root-b");
        var workflowA = Asset(AssetType.Workflow, "workflow-a");
        var workflowB = Asset(AssetType.Workflow, "workflow-b");
        var resolverA = Resolver(rootA, workflowA);
        var resolverB = Resolver(rootB, workflowB);

        (await resolverA.ResolveAsync(Reference(workflowA))).Should().BeSameAs(workflowA);
        (await resolverA.ResolveAsync(Reference(workflowB))).Should().BeNull();
        (await resolverB.ResolveAsync(Reference(workflowB))).Should().BeSameAs(workflowB);
        (await resolverB.ResolveAsync(Reference(workflowA))).Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ShouldResolveEverySupportedGraphResidentAssetType()
    {
        foreach (var type in SupportedTypes)
        {
            var root = Asset(AssetType.Project, $"root-{type}");
            var target = type == AssetType.Project
                ? root
                : Asset(type, $"target-{type}");
            var resolver = type == AssetType.Project
                ? Resolver(root)
                : Resolver(root, target);

            var result = await resolver.ResolveAsync(Reference(target));

            result.Should().BeSameAs(target, $"{type} is a supported schema-v1 graph asset type");
        }
    }

    [Fact]
    public void Resolver_ShouldImplementExistingAssetResolverContractOnly()
    {
        typeof(GraphBackedAssetResolver).GetInterfaces()
            .Should().Equal(typeof(IAssetResolver));
    }

    [Fact]
    public void ResolveAsync_ShouldCompleteSynchronouslyForInMemoryLookup()
    {
        var root = Asset(AssetType.Project, "root");
        var resolver = Resolver(root);

        var result = resolver.ResolveAsync(Reference(root));

        result.IsCompletedSuccessfully.Should().BeTrue();
        result.Result.Should().BeSameAs(root);
    }

    private static GraphBackedAssetResolver Resolver(params StubAsset[] assets)
    {
        var root = assets.First(static asset => asset.Type == AssetType.Project);
        var nodes = assets.Select(static asset =>
            new AIAssetGraphNode(AssetDefinitionKey.From(asset), asset));
        var graph = new AIAssetGraph(
            AssetDefinitionKey.From(root),
            nodes,
            Array.Empty<AIAssetGraphRelationship>());

        return new GraphBackedAssetResolver(graph);
    }

    private static StubAsset Asset(AssetType type, string name) =>
        new(
            type,
            AssetId.New(),
            new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{name}"),
            new AssetVersion("1.0.0"),
            new AssetMetadata { Name = name });

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private sealed class StubAsset(
        AssetType type,
        AssetId id,
        AssetUrn urn,
        AssetVersion version,
        AssetMetadata metadata) : IAsset
    {
        public AssetId Id { get; } = id;

        public AssetUrn Urn { get; } = urn;

        public AssetVersion Version { get; } = version;

        public AssetMetadata Metadata { get; } = metadata;

        public AssetType Type { get; } = type;

        public AssetLifecycle Lifecycle => AssetLifecycle.Published;

        public IReadOnlyCollection<AssetReference> References { get; } = Array.Empty<AssetReference>();

        public IReadOnlyCollection<AssetDependency> Dependencies { get; } = Array.Empty<AssetDependency>();
    }
}
