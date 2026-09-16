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
    private readonly IWorkflowRuntime _workflowRuntime;
    private readonly ApplicationInvocationCoordinationAuthority _coordinationAuthority;

    public ApplicationInvoker(
        IWorkflowRuntime workflowRuntime,
        ApplicationInvocationCoordinationAuthority coordinationAuthority)
    {
        ArgumentNullException.ThrowIfNull(workflowRuntime);
        ArgumentNullException.ThrowIfNull(coordinationAuthority);

        _workflowRuntime = workflowRuntime;
        _coordinationAuthority = coordinationAuthority;
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

        if (primaryFailure is not null)
        {
            // B.5B.3 adds best-effort secondary reporting when release also detects
            // an invariant violation. The admitted invocation's failure remains primary.
            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }

        if (!release.Released)
        {
            throw new InvalidOperationException(
                $"Application invocation coordination release invariant violated: " +
                $"expected Active -> Idle but observed state {release.ObservedState}.");
        }

        return result!;
    }
}
