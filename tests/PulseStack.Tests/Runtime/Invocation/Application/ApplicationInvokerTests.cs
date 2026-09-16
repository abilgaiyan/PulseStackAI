using System.Reflection;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Workflows;
using PulseStack.Core.Runtime.Invocation.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Invocation.Application;

public sealed class ApplicationInvokerTests
{
    [Fact]
    public async Task InvokeAsync_ShouldRequireApplicationAndRequest()
    {
        var runtime = new StubWorkflowRuntime();
        var invoker = Invoker(runtime);
        var application = Application();
        var request = new ApplicationInvocationRequest("input");

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => invoker.InvokeAsync(null!, request));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => invoker.InvokeAsync(application, null!));

        Assert.Equal(0, runtime.CallCount);
    }

    [Fact]
    public async Task InvokeAsync_ShouldGivePreAdmissionCancellationPrecedenceOverOccupancy()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var runtime = new StubWorkflowRuntime();
        var invoker = new ApplicationInvoker(runtime, authority);
        var application = Application();
        var request = new ApplicationInvocationRequest("input");
        using var cancellation = new CancellationTokenSource();

        Assert.True(authority.TryAcquire(application, out var ownership));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => invoker.InvokeAsync(application, request, cancellation.Token));

        Assert.Equal(0, runtime.CallCount);
        Assert.True(ownership!.Release().Released);
    }

    [Fact]
    public async Task InvokeAsync_ShouldRejectOverlapBeforeWorkflowRuntimeEntry()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new StubWorkflowRuntime
        {
            Handler = async (_, _, cancellationToken) =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(cancellationToken);
                return Success("first");
            }
        };
        var invoker = Invoker(runtime);
        var application = Application();
        var request = new ApplicationInvocationRequest("input");

        var first = invoker.InvokeAsync(application, request);
        await entered.Task;

        var contention = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync(application, request));

        Assert.Contains("active invocation", contention.Message, StringComparison.Ordinal);
        Assert.Equal(1, runtime.CallCount);

        release.TrySetResult();
        var firstResult = await first;

        Assert.Equal("first", firstResult.FinalOutput);
    }

    [Fact]
    public async Task InvokeAsync_ShouldProjectRequestExecuteWorkflowAndProjectResult()
    {
        Workflow? observedWorkflow = null;
        PipelineContext? observedContext = null;
        var runtime = new StubWorkflowRuntime
        {
            Handler = (workflow, context, _) =>
            {
                observedWorkflow = workflow;
                observedContext = context;
                return Task.FromResult(Success("output"));
            }
        };
        var invoker = Invoker(runtime);
        var application = Application();
        var request = new ApplicationInvocationRequest(
            "input",
            new Dictionary<string, object?> { ["tenant"] = "north" });

        var result = await invoker.InvokeAsync(application, request);

        Assert.Same(application.Workflow, observedWorkflow);
        Assert.NotNull(observedContext);
        Assert.Equal("input", observedContext.Input);
        Assert.Equal("input", observedContext.CurrentOutput);
        Assert.Equal("north", observedContext.Items["tenant"]);
        Assert.Same(application.Project, result.Project);
        Assert.Same(application.EntryWorkflow, result.EntryWorkflow);
        Assert.True(result.Success);
        Assert.Equal("output", result.FinalOutput);
        Assert.Equal(1, runtime.CallCount);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReleaseOwnershipAfterWorkflowFailure()
    {
        var failure = new InvalidOperationException("workflow failed");
        var runtime = new StubWorkflowRuntime
        {
            Handler = (_, _, _) => Task.FromException<WorkflowExecutionResult>(failure)
        };
        var invoker = Invoker(runtime);
        var application = Application();
        var request = new ApplicationInvocationRequest("input");

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync(application, request));
        Assert.Same(failure, observed);

        runtime.Handler = (_, _, _) => Task.FromResult(Success("recovered"));
        var recovered = await invoker.InvokeAsync(application, request);

        Assert.Equal("recovered", recovered.FinalOutput);
        Assert.Equal(2, runtime.CallCount);
    }

    [Fact]
    public async Task InvokeAsync_ShouldReleaseOwnershipAfterCancellation()
    {
        var runtime = new StubWorkflowRuntime
        {
            Handler = async (_, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return Success("unreachable");
            }
        };
        var invoker = Invoker(runtime);
        var application = Application();
        var request = new ApplicationInvocationRequest("input");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => invoker.InvokeAsync(application, request, cancellation.Token));

        runtime.Handler = (_, _, _) => Task.FromResult(Success("recovered"));
        var recovered = await invoker.InvokeAsync(application, request);

        Assert.Equal("recovered", recovered.FinalOutput);
        Assert.Equal(1, runtime.CallCount);
    }

    [Fact]
    public async Task InvokeAsync_ShouldAllowEquivalentDistinctApplicationsToOverlap()
    {
        var project = Reference(AssetType.Project, "shared");
        var entryWorkflow = Reference(AssetType.Workflow, "shared");
        var workflow = new Workflow("entry");
        var first = Application(project, entryWorkflow, workflow);
        var second = Application(project, entryWorkflow, workflow);
        var entered = 0;
        var bothEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runtime = new StubWorkflowRuntime
        {
            Handler = async (_, _, cancellationToken) =>
            {
                if (Interlocked.Increment(ref entered) == 2)
                {
                    bothEntered.TrySetResult();
                }

                await release.Task.WaitAsync(cancellationToken);
                return Success("output");
            }
        };
        var invoker = Invoker(runtime);
        var request = new ApplicationInvocationRequest("input");

        var firstInvocation = invoker.InvokeAsync(first, request);
        var secondInvocation = invoker.InvokeAsync(second, request);

        await bothEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        release.TrySetResult();
        await Task.WhenAll(firstInvocation, secondInvocation);

        Assert.Equal(2, runtime.CallCount);
    }

    private static ApplicationInvoker Invoker(StubWorkflowRuntime runtime) =>
        new(runtime, new ApplicationInvocationCoordinationAuthority());

    private static WorkflowExecutionResult Success(string output) =>
        new()
        {
            Success = true,
            FinalOutput = output,
            Steps = []
        };

    private static RealizedApplication Application(string identity = "application") =>
        Application(
            Reference(AssetType.Project, identity),
            Reference(AssetType.Workflow, identity),
            new Workflow("entry"));

    private static AssetReference Reference(AssetType type, string identity) =>
        new(
            type,
            AssetId.New(),
            new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{identity}"),
            new AssetVersion("1.0.0"));

    private static RealizedApplication Application(
        AssetReference project,
        AssetReference entryWorkflow,
        Workflow workflow)
    {
        var constructor = typeof(RealizedApplication).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();

        return (RealizedApplication)constructor.Invoke(
            [project, entryWorkflow, workflow]);
    }

    private sealed class StubWorkflowRuntime : IWorkflowRuntime
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Func<Workflow, PipelineContext, CancellationToken, Task<WorkflowExecutionResult>> Handler { get; set; } =
            (_, _, _) => Task.FromResult(Success("output"));

        public Task<WorkflowExecutionResult> ExecuteAsync(
            Workflow workflow,
            PipelineContext context,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _callCount);
            return Handler(workflow, context, cancellationToken);
        }
    }
}
