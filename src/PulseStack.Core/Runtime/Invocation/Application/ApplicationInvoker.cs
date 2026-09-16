using System.Runtime.ExceptionServices;
using PulseStack.Abstractions.Runtime.Invocation.Application;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Runtime.Realization.Application;

namespace PulseStack.Core.Runtime.Invocation.Application;

/// <summary>
/// Coordinates portable invocation of a realized application through the existing workflow runtime.
/// </summary>
internal sealed class ApplicationInvoker : IApplicationInvoker
{
    private const string ExpectedReleaseTransition = "Active -> Idle";

    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly ApplicationInvocationCoordinationAuthority _coordinationAuthority;
    private readonly IApplicationInvocationReleaseInvariantReporter _releaseInvariantReporter;

    public ApplicationInvoker(
        IWorkflowRuntime workflowRuntime,
        ApplicationInvocationCoordinationAuthority coordinationAuthority)
        : this(
            workflowRuntime,
            coordinationAuthority,
            NullApplicationInvocationReleaseInvariantReporter.Instance)
    {
    }

    internal ApplicationInvoker(
        IWorkflowRuntime workflowRuntime,
        ApplicationInvocationCoordinationAuthority coordinationAuthority,
        IApplicationInvocationReleaseInvariantReporter releaseInvariantReporter)
    {
        ArgumentNullException.ThrowIfNull(workflowRuntime);
        ArgumentNullException.ThrowIfNull(coordinationAuthority);
        ArgumentNullException.ThrowIfNull(releaseInvariantReporter);

        _workflowRuntime = workflowRuntime;
        _coordinationAuthority = coordinationAuthority;
        _releaseInvariantReporter = releaseInvariantReporter;
    }

    public async Task<ApplicationInvocationResult> InvokeAsync(
        RealizedApplication application,
        ApplicationInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(request);

        cancellationToken.ThrowIfCancellationRequested();

        if (!_coordinationAuthority.TryAcquire(application, out var ownership))
        {
            throw new InvalidOperationException(
                "The realized application already has an active invocation.");
        }

        ApplicationInvocationResult? result = null;
        Exception? primaryFailure = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = ApplicationInvocationContextProjector.Project(request);
            var executionResult = await _workflowRuntime
                .ExecuteAsync(application.Workflow, context, cancellationToken)
                .ConfigureAwait(false);

            result = ApplicationInvocationResultProjector.Project(
                application,
                executionResult);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
        }

        var release = ownership!.Release();
        Exception? releaseInvariantFailure = null;

        if (!release.Released)
        {
            releaseInvariantFailure = CreateReleaseInvariantFailure(release.ObservedState);
        }

        if (primaryFailure is not null)
        {
            if (releaseInvariantFailure is not null)
            {
                ReportSecondaryReleaseInvariant(
                    application,
                    release.ObservedState,
                    releaseInvariantFailure);
            }

            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }

        if (releaseInvariantFailure is not null)
        {
            ExceptionDispatchInfo.Capture(releaseInvariantFailure).Throw();
        }

        return result!;
    }

    private static InvalidOperationException CreateReleaseInvariantFailure(int observedState) =>
        new(
            $"Application invocation coordination release invariant violated: " +
            $"expected {ExpectedReleaseTransition} but observed state {observedState}.");

    private void ReportSecondaryReleaseInvariant(
        RealizedApplication application,
        int observedState,
        Exception failure)
    {
        var diagnostic = new ApplicationInvocationReleaseInvariantDiagnostic(
            ApplicationInvocationCoordinationFailureKind.ReleaseInvariantViolation,
            ExpectedReleaseTransition,
            observedState,
            application.Project,
            application.EntryWorkflow,
            failure);

        try
        {
            _releaseInvariantReporter.Report(diagnostic);
        }
        catch
        {
            // Secondary diagnostics must never replace the admitted invocation's
            // already-primary exception or cancellation.
        }
    }
}
