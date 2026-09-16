using PulseStack.Abstractions.Assets;

namespace PulseStack.Core.Runtime.Invocation.Application;

internal enum ApplicationInvocationCoordinationFailureKind
{
    ReleaseInvariantViolation = 1
}

internal readonly record struct ApplicationInvocationReleaseInvariantDiagnostic(
    ApplicationInvocationCoordinationFailureKind FailureKind,
    string ExpectedTransition,
    int ObservedState,
    AssetReference Project,
    AssetReference EntryWorkflow,
    Exception Failure);

internal interface IApplicationInvocationReleaseInvariantReporter
{
    void Report(ApplicationInvocationReleaseInvariantDiagnostic diagnostic);
}

internal sealed class NullApplicationInvocationReleaseInvariantReporter
    : IApplicationInvocationReleaseInvariantReporter
{
    public static NullApplicationInvocationReleaseInvariantReporter Instance { get; } = new();

    private NullApplicationInvocationReleaseInvariantReporter()
    {
    }

    public void Report(ApplicationInvocationReleaseInvariantDiagnostic diagnostic)
    {
    }
}
