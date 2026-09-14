using System.Reflection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Runtime.Realization.Composition;
using PulseStack.Abstractions.Runtime.Realization.Resolution;
using PulseStack.Abstractions.Workflows;
using PulseStack.Core.Runtime.Realization.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Realization.Application;

public sealed class ApplicationRealizerTests
{
    [Fact]
    public void Constructor_ShouldRequireChainFactory()
    {
        Assert.Throws<ArgumentNullException>(() => new ApplicationRealizer(null!));
    }

    [Fact]
    public async Task RealizeAsync_ShouldObserveCancellationBeforeNullGraphValidation()
    {
        var realizer = new ApplicationRealizer(new RecordingFactory(new RecordingComposer(new Workflow("unused"))));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => realizer.RealizeAsync(null!, cancellation.Token));
    }

    [Fact]
    public async Task RealizeAsync_ShouldRejectNullGraphWhenNotCancelled()
    {
        var realizer = new ApplicationRealizer(new RecordingFactory(new RecordingComposer(new Workflow("unused"))));

        await Assert.ThrowsAsync<ArgumentNullException>(() => realizer.RealizeAsync(null!));
    }

    [Theory]
    [InlineData(AssetType.Library)]
    [InlineData(AssetType.Package)]
    public async Task RealizeAsync_ShouldReturnUnsupportedRootWithoutCreatingChain(AssetType rootType)
    {
        var root = Stub(rootType, "root");
        var graph = Graph(root);
        var factory = new RecordingFactory(new RecordingComposer(new Workflow("unused")));
        var realizer = new ApplicationRealizer(factory);

        var result = await realizer.RealizeAsync(graph);

        var unsupported = Assert.IsType<ApplicationRealizationResult.UnsupportedRoot>(result);
        Assert.Equal(graph.RootKey, unsupported.Context.RootKey);
        Assert.Equal(0, factory.Calls);
    }

    [Fact]
    public async Task RealizeAsync_ShouldRejectProjectRootConcreteTypeIncoherenceWithoutCreatingChain()
    {
        var root = Stub(AssetType.Project, "root");
        var graph = Graph(root);
        var factory = new RecordingFactory(new RecordingComposer(new Workflow("unused")));
        var realizer = new ApplicationRealizer(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() => realizer.RealizeAsync(graph));

        Assert.Equal(0, factory.Calls);
    }

    [Fact]
    public async Task RealizeAsync_ShouldReturnUnresolvedWithExactAuthoredEntryReference()
    {
        var missing = Stub(AssetType.Workflow, "missing");
        var entry = Reference(missing);
        var project = Project(entry);
        var graph = Graph(project);
        var factory = new RecordingFactory(new RecordingComposer(new Workflow("unused")));
        var realizer = new ApplicationRealizer(factory);

        var result = await realizer.RealizeAsync(graph);

        var unresolved = Assert.IsType<ApplicationRealizationResult.EntryWorkflowUnresolved>(result);
        Assert.Equal(graph.RootKey, unresolved.Context.RootKey);
        Assert.Same(entry, unresolved.Context.EntryWorkflow);
        Assert.Equal(0, factory.Calls);
    }

    [Fact]
    public async Task RealizeAsync_ShouldReturnTypeIncoherentWhenResolvedAssetIsNotWorkflowAsset()
    {
        var incoherent = Stub(AssetType.Workflow, "entry");
        var entry = Reference(incoherent);
        var project = Project(entry);
        var graph = Graph(project, incoherent);
        var factory = new RecordingFactory(new RecordingComposer(new Workflow("unused")));
        var realizer = new ApplicationRealizer(factory);

        var result = await realizer.RealizeAsync(graph);

        var typed = Assert.IsType<ApplicationRealizationResult.EntryWorkflowTypeIncoherent>(result);
        Assert.Equal(graph.RootKey, typed.Context.RootKey);
        Assert.Same(entry, typed.Context.EntryWorkflow);
        Assert.Equal(0, factory.Calls);
    }

    [Fact]
    public async Task RealizeAsync_ShouldSelectExactEntryAndDelegateOnceThroughGraphResolver()
    {
        var entryWorkflow = WorkflowAsset("entry");
        var alternateWorkflow = WorkflowAsset("alternate");
        var outsider = WorkflowAsset("outside");
        var entry = Reference(entryWorkflow);
        var project = Project(entry, Reference(alternateWorkflow));
        var graph = Graph(project, alternateWorkflow, entryWorkflow);
        var returnedWorkflow = new Workflow("realized-entry");
        var composer = new RecordingComposer(returnedWorkflow);
        var factory = new RecordingFactory(composer);
        var realizer = new ApplicationRealizer(factory);
        using var cancellation = new CancellationTokenSource();
        var token = cancellation.Token;

        var result = await realizer.RealizeAsync(graph, token);

        var success = Assert.IsType<ApplicationRealizationResult.Success>(result);
        Assert.Same(returnedWorkflow, success.Workflow);
        Assert.Equal(1, factory.Calls);
        Assert.NotNull(factory.Resolver);
        Assert.IsType<GraphBackedAssetResolver>(factory.Resolver);
        Assert.Same(entryWorkflow, await factory.Resolver!.ResolveAsync(entry));
        Assert.Same(alternateWorkflow, await factory.Resolver.ResolveAsync(Reference(alternateWorkflow)));
        Assert.Null(await factory.Resolver.ResolveAsync(Reference(outsider)));
        Assert.Equal(1, composer.Calls);
        Assert.Same(entryWorkflow, composer.Workflow);
        Assert.Equal(token, composer.CancellationToken);
    }

    [Fact]
    public async Task RealizeAsync_ShouldPropagateFactoryExceptionUnchanged()
    {
        var workflow = WorkflowAsset("entry");
        var entry = Reference(workflow);
        var graph = Graph(Project(entry), workflow);
        var expected = new InvalidOperationException("factory failure");
        var factory = new RecordingFactory(new RecordingComposer(new Workflow("unused")))
        {
            ExceptionToThrow = expected
        };
        var realizer = new ApplicationRealizer(factory);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => realizer.RealizeAsync(graph));

        Assert.Same(expected, actual);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public async Task RealizeAsync_ShouldPropagateComposerExceptionUnchanged()
    {
        var workflow = WorkflowAsset("entry");
        var entry = Reference(workflow);
        var graph = Graph(Project(entry), workflow);
        var expected = new InvalidOperationException("composer failure");
        var composer = new RecordingComposer(new Workflow("unused"))
        {
            ExceptionToThrow = expected
        };
        var factory = new RecordingFactory(composer);
        var realizer = new ApplicationRealizer(factory);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => realizer.RealizeAsync(graph));

        Assert.Same(expected, actual);
        Assert.Equal(1, factory.Calls);
        Assert.Equal(1, composer.Calls);
    }

    [Fact]
    public void ApplicationRealizer_ShouldDependOnlyOnFrozenChainFactoryContract()
    {
        var constructor = typeof(ApplicationRealizer).GetConstructors().Single();
        var parameters = constructor.GetParameters();

        Assert.Single(parameters);
        Assert.Equal(typeof(IApplicationRealizationChainFactory), parameters[0].ParameterType);
    }

    private static AIAssetGraph Graph(IAsset root, params IAsset[] additional)
    {
        var assets = new[] { root }.Concat(additional);
        return new AIAssetGraph(
            AssetDefinitionKey.From(root),
            assets.Select(static asset => new AIAssetGraphNode(AssetDefinitionKey.From(asset), asset)),
            Array.Empty<AIAssetGraphRelationship>());
    }

    private static ProjectAsset Project(AssetReference entryWorkflow, params AssetReference[] additionalOwned)
    {
        var options = new ProjectAssetOptions
        {
            Name = "project",
            EntryWorkflow = entryWorkflow,
            OwnedAssets = new[] { entryWorkflow }.Concat(additionalOwned).ToArray()
        };

        var constructor = typeof(ProjectAsset)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(static constructor =>
            {
                var parameters = constructor.GetParameters();
                return parameters.Length == 4
                    && parameters[0].ParameterType == typeof(AssetId)
                    && parameters[1].ParameterType == typeof(AssetUrn)
                    && parameters[2].ParameterType == typeof(ProjectAssetOptions);
            });

        return (ProjectAsset)constructor.Invoke(
            [AssetId.New(), new AssetUrn("urn:pulsestack:project:project"), options, null]);
    }

    private static WorkflowAsset WorkflowAsset(string name)
    {
        var options = new WorkflowAssetOptions { Name = name };
        var constructor = typeof(WorkflowAsset)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(static constructor =>
            {
                var parameters = constructor.GetParameters();
                return parameters.Length == 3
                    && parameters[0].ParameterType == typeof(AssetId)
                    && parameters[1].ParameterType == typeof(AssetUrn)
                    && parameters[2].ParameterType == typeof(WorkflowAssetOptions);
            });

        return (WorkflowAsset)constructor.Invoke(
            [AssetId.New(), new AssetUrn($"urn:pulsestack:workflow:{name}"), options]);
    }

    private static StubAsset Stub(AssetType type, string name) =>
        new(
            type,
            AssetId.New(),
            new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{name}"),
            AssetVersion.Initial,
            new AssetMetadata { Name = name });

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private sealed class RecordingFactory(IWorkflowComposer composer) : IApplicationRealizationChainFactory
    {
        public int Calls { get; private set; }
        public IAssetResolver? Resolver { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        public IWorkflowComposer Create(IAssetResolver assetResolver)
        {
            Calls++;
            Resolver = assetResolver;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return composer;
        }
    }

    private sealed class RecordingComposer(Workflow result) : IWorkflowComposer
    {
        public int Calls { get; private set; }
        public WorkflowAsset? Workflow { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        public Task<Workflow> ComposeAsync(
            WorkflowAsset workflow,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            Workflow = workflow;
            CancellationToken = cancellationToken;

            return ExceptionToThrow is null
                ? Task.FromResult(result)
                : Task.FromException<Workflow>(ExceptionToThrow);
        }
    }

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
