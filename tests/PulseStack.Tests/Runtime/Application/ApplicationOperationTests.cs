using System.Runtime.CompilerServices;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Runtime.Application;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Workflows;
using PulseStack.Agents.Runtime.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Application;

public sealed class ApplicationOperationTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldStopAtLoadFailureAndPreserveExactResult()
    {
        var loadFailure = new AIAssetGraphLoadResult.RootDefinitionUnavailable(
            new AIAssetGraphRootDefinitionUnavailableContext(Key(AssetType.Project)));
        var loader = new StubGraphLoader(loadFailure);
        var realizer = new StubRealizer(CreateRealizationSuccess());
        var invoker = new StubInvoker(CreateInvocationResult(true));
        var operation = new ApplicationOperation(loader, realizer, invoker);

        var result = await operation.ExecuteAsync(Key(AssetType.Project), Request());

        var outcome = result.Should().BeOfType<ApplicationOperationResult.LoadOutcome>().Subject;
        outcome.Result.Should().BeSameAs(loadFailure);
        loader.CallCount.Should().Be(1);
        realizer.CallCount.Should().Be(0);
        invoker.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStopAtRealizationFailureAndPreserveExactResult()
    {
        var graph = Graph();
        var realizationFailure = new ApplicationRealizationResult.UnsupportedRoot(
            new ApplicationRealizationUnsupportedRootContext(Key(AssetType.Library)));
        var loader = new StubGraphLoader(new AIAssetGraphLoadResult.Success(graph));
        var realizer = new StubRealizer(realizationFailure);
        var invoker = new StubInvoker(CreateInvocationResult(true));
        var operation = new ApplicationOperation(loader, realizer, invoker);

        var result = await operation.ExecuteAsync(Key(AssetType.Project), Request());

        var outcome = result.Should().BeOfType<ApplicationOperationResult.RealizationOutcome>().Subject;
        outcome.Result.Should().BeSameAs(realizationFailure);
        realizer.ObservedGraph.Should().BeSameAs(graph);
        invoker.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_ShouldInvokeAndPreserveExactInvocationResult(bool success)
    {
        var graph = Graph();
        var application = RealizedApplication();
        var invocationResult = CreateInvocationResult(success);
        var loader = new StubGraphLoader(new AIAssetGraphLoadResult.Success(graph));
        var realizer = new StubRealizer(new ApplicationRealizationResult.Success(application));
        var invoker = new StubInvoker(invocationResult);
        var operation = new ApplicationOperation(loader, realizer, invoker);
        var request = Request();

        var result = await operation.ExecuteAsync(Key(AssetType.Project), request);

        var outcome = result.Should().BeOfType<ApplicationOperationResult.InvocationOutcome>().Subject;
        outcome.Result.Should().BeSameAs(invocationResult);
        realizer.ObservedGraph.Should().BeSameAs(graph);
        invoker.ObservedApplication.Should().BeSameAs(application);
        invoker.ObservedRequest.Should().BeSameAs(request);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPassTheSameCancellationTokenToEveryReachedStage()
    {
        using var source = new CancellationTokenSource();
        var loader = new StubGraphLoader(new AIAssetGraphLoadResult.Success(Graph()));
        var realizer = new StubRealizer(CreateRealizationSuccess());
        var invoker = new StubInvoker(CreateInvocationResult(true));
        var operation = new ApplicationOperation(loader, realizer, invoker);

        await operation.ExecuteAsync(Key(AssetType.Project), Request(), source.Token);

        loader.ObservedToken.Should().Be(source.Token);
        realizer.ObservedToken.Should().Be(source.Token);
        invoker.ObservedToken.Should().Be(source.Token);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateStageExceptionsWithoutReachingLaterStages()
    {
        var expected = new InvalidOperationException("load failure");
        var loader = new StubGraphLoader(expected);
        var realizer = new StubRealizer(CreateRealizationSuccess());
        var invoker = new StubInvoker(CreateInvocationResult(true));
        var operation = new ApplicationOperation(loader, realizer, invoker);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => operation.ExecuteAsync(Key(AssetType.Project), Request()));

        thrown.Should().BeSameAs(expected);
        realizer.CallCount.Should().Be(0);
        invoker.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateRealizationExceptionWithoutInvoking()
    {
        var expected = new InvalidOperationException("realization failure");
        var loader = new StubGraphLoader(new AIAssetGraphLoadResult.Success(Graph()));
        var realizer = new StubRealizer(expected);
        var invoker = new StubInvoker(CreateInvocationResult(true));
        var operation = new ApplicationOperation(loader, realizer, invoker);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => operation.ExecuteAsync(Key(AssetType.Project), Request()));

        thrown.Should().BeSameAs(expected);
        invoker.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateInvocationException()
    {
        var expected = new InvalidOperationException("invocation failure");
        var loader = new StubGraphLoader(new AIAssetGraphLoadResult.Success(Graph()));
        var realizer = new StubRealizer(CreateRealizationSuccess());
        var invoker = new StubInvoker(expected);
        var operation = new ApplicationOperation(loader, realizer, invoker);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => operation.ExecuteAsync(Key(AssetType.Project), Request()));

        thrown.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPerformEveryStageAgainForRepeatedCalls()
    {
        var loader = new StubGraphLoader(new AIAssetGraphLoadResult.Success(Graph()));
        var realizer = new StubRealizer(CreateRealizationSuccess());
        var invoker = new StubInvoker(CreateInvocationResult(true));
        var operation = new ApplicationOperation(loader, realizer, invoker);
        var key = Key(AssetType.Project);
        var request = Request();

        await operation.ExecuteAsync(key, request);
        await operation.ExecuteAsync(key, request);

        loader.CallCount.Should().Be(2);
        realizer.CallCount.Should().Be(2);
        invoker.CallCount.Should().Be(2);
    }

    private static AIAssetGraph Graph() =>
        (AIAssetGraph)RuntimeHelpers.GetUninitializedObject(typeof(AIAssetGraph));

    private static ApplicationRealizationResult.Success CreateRealizationSuccess() =>
        new(RealizedApplication());

    private static RealizedApplication RealizedApplication() =>
        new(
            Reference(AssetType.Project),
            Reference(AssetType.Workflow),
            new Workflow("entry"));

    private static ApplicationInvocationResult CreateInvocationResult(bool success) =>
        new(
            Reference(AssetType.Project),
            Reference(AssetType.Workflow),
            success,
            "output",
            Array.Empty<PulseStack.Abstractions.Workflows.Steps.StepExecutionResult>());

    private static ApplicationInvocationRequest Request() => new("input");

    private static AssetDefinitionKey Key(AssetType type) =>
        new(type, AssetId.New(), AssetVersion.Initial);

    private static AssetReference Reference(AssetType type) =>
        new(type, AssetId.New(), new AssetUrn($"urn:pulsestack:test:{Guid.NewGuid():N}"), AssetVersion.Initial);

    private sealed class StubGraphLoader : IAIAssetGraphLoader
    {
        private readonly AIAssetGraphLoadResult? _result;
        private readonly Exception? _exception;

        public StubGraphLoader(AIAssetGraphLoadResult result) => _result = result;
        public StubGraphLoader(Exception exception) => _exception = exception;

        public int CallCount { get; private set; }
        public CancellationToken ObservedToken { get; private set; }

        public ValueTask<AIAssetGraphLoadResult> LoadAsync(
            AssetDefinitionKey rootKey,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ObservedToken = cancellationToken;
            if (_exception is not null) throw _exception;
            return ValueTask.FromResult(_result!);
        }
    }

    private sealed class StubRealizer : IApplicationRealizer
    {
        private readonly ApplicationRealizationResult? _result;
        private readonly Exception? _exception;

        public StubRealizer(ApplicationRealizationResult result) => _result = result;
        public StubRealizer(Exception exception) => _exception = exception;

        public int CallCount { get; private set; }
        public AIAssetGraph? ObservedGraph { get; private set; }
        public CancellationToken ObservedToken { get; private set; }

        public Task<ApplicationRealizationResult> RealizeAsync(
            AIAssetGraph graph,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ObservedGraph = graph;
            ObservedToken = cancellationToken;
            if (_exception is not null) throw _exception;
            return Task.FromResult(_result!);
        }
    }

    private sealed class StubInvoker : IApplicationInvoker
    {
        private readonly ApplicationInvocationResult? _result;
        private readonly Exception? _exception;

        public StubInvoker(ApplicationInvocationResult result) => _result = result;
        public StubInvoker(Exception exception) => _exception = exception;

        public int CallCount { get; private set; }
        public RealizedApplication? ObservedApplication { get; private set; }
        public ApplicationInvocationRequest? ObservedRequest { get; private set; }
        public CancellationToken ObservedToken { get; private set; }

        public Task<ApplicationInvocationResult> InvokeAsync(
            RealizedApplication application,
            ApplicationInvocationRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ObservedApplication = application;
            ObservedRequest = request;
            ObservedToken = cancellationToken;
            if (_exception is not null) throw _exception;
            return Task.FromResult(_result!);
        }
    }
}
