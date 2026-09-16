using PulseStack.Abstractions.Assets;
using PulseStack.Core.Runtime.Invocation.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Invocation.Application;

public sealed class ApplicationInvocationReleaseInvariantDiagnosticsTests
{
    [Fact]
    public void Diagnostic_ShouldCarryMinimumInvocationPrivateContract()
    {
        var project = Reference(AssetType.Project, "project");
        var entryWorkflow = Reference(AssetType.Workflow, "workflow");
        var failure = new InvalidOperationException("release invariant");

        var diagnostic = new ApplicationInvocationReleaseInvariantDiagnostic(
            ApplicationInvocationCoordinationFailureKind.ReleaseInvariantViolation,
            "Active -> Idle",
            ApplicationInvocationCoordinationAuthority.OccupancyCell.Idle,
            project,
            entryWorkflow,
            failure);

        Assert.Equal(
            ApplicationInvocationCoordinationFailureKind.ReleaseInvariantViolation,
            diagnostic.FailureKind);
        Assert.Equal("Active -> Idle", diagnostic.ExpectedTransition);
        Assert.Equal(
            ApplicationInvocationCoordinationAuthority.OccupancyCell.Idle,
            diagnostic.ObservedState);
        Assert.Same(project, diagnostic.Project);
        Assert.Same(entryWorkflow, diagnostic.EntryWorkflow);
        Assert.Same(failure, diagnostic.Failure);
    }

    [Fact]
    public void NullReporter_ShouldAcceptDiagnosticWithoutFailure()
    {
        var diagnostic = new ApplicationInvocationReleaseInvariantDiagnostic(
            ApplicationInvocationCoordinationFailureKind.ReleaseInvariantViolation,
            "Active -> Idle",
            ApplicationInvocationCoordinationAuthority.OccupancyCell.Idle,
            Reference(AssetType.Project, "project"),
            Reference(AssetType.Workflow, "workflow"),
            new InvalidOperationException("release invariant"));

        var failure = Record.Exception(
            () => NullApplicationInvocationReleaseInvariantReporter.Instance.Report(diagnostic));

        Assert.Null(failure);
    }

    private static AssetReference Reference(AssetType type, string identity) =>
        new(
            type,
            AssetId.New(),
            new AssetUrn($"urn:pulsestack:{type.ToString().ToLowerInvariant()}:{identity}"),
            new AssetVersion("1.0.0"));
}
