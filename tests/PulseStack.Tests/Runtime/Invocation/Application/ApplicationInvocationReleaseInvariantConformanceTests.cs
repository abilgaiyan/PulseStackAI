using System.Reflection;
using System.Runtime.CompilerServices;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Workflows;
using PulseStack.Core.Runtime.Invocation.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Invocation.Application;

public sealed class ApplicationInvocationReleaseInvariantConformanceTests
{
    [Fact]
    public async Task InvokeAsync_ShouldSurfaceReleaseInvariantWhenInvocationOtherwiseSucceeds()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var reporter = new RecordingReporter();
        var application = Application();
        var runtime = Runtime((_, _, _) =>
        {
            CorruptReleaseState(authority, application);
            return Task.FromResult(Success("output"));
        });
        var invoker = new ApplicationInvoker(runtime, authority, reporter);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync(application, Request()));

        Assert.Contains("release invariant violated", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Active -> Idle", failure.Message, StringComparison.Ordinal);
        Assert.Empty(reporter.Diagnostics);
    }

    [Fact]
    public async Task InvokeAsync_ShouldPreserveInvocationExceptionAndReportCorruptReleaseOnce()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var reporter = new RecordingReporter();
        var application = Application();
        var primary = new InvalidOperationException("workflow failed");
        var runtime = Runtime((_, _, _) =>
        {
            CorruptReleaseState(authority, application);
            return Task.FromException<WorkflowExecutionResult>(primary);
        });
        var invoker = new ApplicationInvoker(runtime, authority, reporter);

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync(application, Request()));

        Assert.Same(primary, observed);
        var diagnostic = Assert.Single(reporter.Diagnostics);
        AssertDiagnostic(diagnostic, application);
    }

    [Fact]
    public async Task InvokeAsync_ShouldPreserveCancellationAndReportCorruptReleaseOnce()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var reporter = new RecordingReporter();
        var application = Application();
        using var cancellation = new CancellationTokenSource();
        var runtime = Runtime((_, _, token) =>
        {
            CorruptReleaseState(authority, application);
            cancellation.Cancel();
            return Task.FromCanceled<WorkflowExecutionResult>(token);
        });
        var invoker = new ApplicationInvoker(runtime, authority, reporter);

        var observed = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => invoker.InvokeAsync(application, Request(), cancellation.Token));

        Assert.Equal(cancellation.Token, observed.CancellationToken);
        var diagnostic = Assert.Single(reporter.Diagnostics);
        AssertDiagnostic(diagnostic, application);
    }

    [Fact]
    public async Task InvokeAsync_ShouldPreserveInvocationExceptionWhenSecondaryReporterFails()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var reporter = new ThrowingReporter();
        var application = Application();
        var primary = new InvalidOperationException("workflow failed");
        var runtime = Runtime((_, _, _) =>
        {
            CorruptReleaseState(authority, application);
            return Task.FromException<WorkflowExecutionResult>(primary);
        });
        var invoker = new ApplicationInvoker(runtime, authority, reporter);

        var observed = await Assert.ThrowsAsync<InvalidOperationException>(
            () => invoker.InvokeAsync(application, Request()));

        Assert.Same(primary, observed);
        Assert.Equal(1, reporter.CallCount);
    }

    [Fact]
    public async Task InvokeAsync_ShouldPreserveCancellationWhenSecondaryReporterFails()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var reporter = new ThrowingReporter();
        var application = Application();
        using var cancellation = new CancellationTokenSource();
        var runtime = Runtime((_, _, token) =>
        {
            CorruptReleaseState(authority, application);
            cancellation.Cancel();
            return Task.FromCanceled<WorkflowExecutionResult>(token);
        });
        var invoker = new ApplicationInvoker(runtime, authority, reporter);

        var observed = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => invoker.InvokeAsync(application, Request(), cancellation.Token));

        Assert.Equal(cancellation.Token, observed.CancellationToken);
        Assert.Equal(1, reporter.CallCount);
    }

    private static void AssertDiagnostic(
        ApplicationInvocationReleaseInvariantDiagnostic diagnostic,
        RealizedApplication application)
    {
        Assert.Equal(
            ApplicationInvocationCoordinationFailureKind.ReleaseInvariantViolation,
            diagnostic.FailureKind);
        Assert.Equal("Active -> Idle", diagnostic.ExpectedTransition);
        Assert.Equal(
            ApplicationInvocationCoordinationAuthority.OccupancyCell.Idle,
            diagnostic.ObservedState);
        Assert.Same(application.Project, diagnostic.Project);
        Assert.Same(application.EntryWorkflow, diagnostic.EntryWorkflow);
        Assert.IsType<InvalidOperationException>(diagnostic.Failure);
    }

    private static void CorruptReleaseState(
        ApplicationInvocationCoordinationAuthority authority,
        RealizedApplication application)
    {
        var occupanciesField = typeof(ApplicationInvocationCoordinationAuthority).GetField(
            "_occupancies",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var occupancies = (ConditionalWeakTable<
            RealizedApplication,
            ApplicationInvocationCoordinationAuthority.OccupancyCell>)occupanciesField.GetValue(authority)!;

        Assert.True(occupancies.TryGetValue(application, out var cell));
        Assert.NotNull(cell);

        var stateField = typeof(ApplicationInvocationCoordinationAuthority.OccupancyCell).GetField(
            "_state",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        stateField.SetValue(cell, ApplicationInvocationCoordinationAuthority.OccupancyCell.Idle);
    }

    private static StubWorkflowRuntime Runtime(
        Func<Workflow, PipelineContext, CancellationToken, Task<WorkflowExecutionResult>> handler) =>
        new() { Handler = handler };

    private static ApplicationInvocationRequest Request() => new("input");

    private static WorkflowExecutionResult Success(string output) =>
        new()
        {
            Success = true,
            FinalOutput = output,
            Steps = []
        };

    private static RealizedApplication Application()
    {
        var constructor = typeof(RealizedApplication).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();

        return (RealizedApplication)constructor.Invoke(
            [
                Reference(AssetType.Project, "project"),
                Reference(AssetType.Workflow, "workflow"),
                new Workflow("entry")
            ]);
    }

    private static AssetReference Reference(AssetType type, string identity) =>
        new(
            type,
            AssetId.New(),
            new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{identity}"),
            new AssetVersion("1.0.0"));

    private sealed class StubWorkflowRuntime : IWorkflowRuntime
    {
        public required Func<Workflow, PipelineContext, CancellationToken, Task<WorkflowExecutionResult>> Handler { get; init; }

        public Task<WorkflowExecutionResult> ExecuteAsync(
            Workflow workflow,
            PipelineContext context,
            CancellationToken cancellationToken = default) =>
            Handler(workflow, context, cancellationToken);
    }

    private sealed class RecordingReporter : IApplicationInvocationReleaseInvariantReporter
    {
        public List<ApplicationInvocationReleaseInvariantDiagnostic> Diagnostics { get; } = [];

        public void Report(ApplicationInvocationReleaseInvariantDiagnostic diagnostic) =>
            Diagnostics.Add(diagnostic);
    }

    private sealed class ThrowingReporter : IApplicationInvocationReleaseInvariantReporter
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public void Report(ApplicationInvocationReleaseInvariantDiagnostic diagnostic)
        {
            Interlocked.Increment(ref _callCount);
            throw new InvalidOperationException("reporter failed");
        }
    }
}
