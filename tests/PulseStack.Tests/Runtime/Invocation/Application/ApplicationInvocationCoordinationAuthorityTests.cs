using System.Reflection;
using System.Runtime.CompilerServices;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Runtime.Realization.Application;
using PulseStack.Abstractions.Workflows;
using PulseStack.Core.Runtime.Invocation.Application;
using Xunit;

namespace PulseStack.Tests.Runtime.Invocation.Application;

public sealed class ApplicationInvocationCoordinationAuthorityTests
{
    [Fact]
    public void TryAcquire_ShouldRequireApplication()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();

        Assert.Throws<ArgumentNullException>(
            () => authority.TryAcquire(null!, out _));
    }

    [Fact]
    public void TryAcquire_ShouldAdmitIdleApplicationAndRejectWhileActive()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var application = Application();

        Assert.True(authority.TryAcquire(application, out var ownership));
        Assert.NotNull(ownership);

        Assert.False(authority.TryAcquire(application, out var rejectedOwnership));
        Assert.Null(rejectedOwnership);

        var release = ownership.Release();
        Assert.True(release.Released);
        Assert.Equal(
            ApplicationInvocationCoordinationAuthority.OccupancyCell.Active,
            release.ObservedState);
    }

    [Fact]
    public void Release_ShouldRestoreAvailabilityForSequentialReuse()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var application = Application();

        Assert.True(authority.TryAcquire(application, out var first));
        Assert.True(first!.Release().Released);

        Assert.True(authority.TryAcquire(application, out var second));
        Assert.NotNull(second);
        Assert.NotSame(first, second);
        Assert.True(second.Release().Released);
    }

    [Fact]
    public void TryAcquire_ShouldUseConcreteObjectIdentity()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var project = Reference(AssetType.Project, "shared");
        var entryWorkflow = Reference(AssetType.Workflow, "shared");
        var workflow = new Workflow("entry");
        var first = Application(project, entryWorkflow, workflow);
        var second = Application(project, entryWorkflow, workflow);

        Assert.NotSame(first, second);
        Assert.Same(first.Project, second.Project);
        Assert.Same(first.EntryWorkflow, second.EntryWorkflow);
        Assert.Same(first.Workflow, second.Workflow);

        Assert.True(authority.TryAcquire(first, out var firstOwnership));
        Assert.True(authority.TryAcquire(second, out var secondOwnership));

        Assert.True(firstOwnership!.Release().Released);
        Assert.True(secondOwnership!.Release().Released);
    }

    [Fact]
    public async Task TryAcquire_ShouldAdmitExactlyOneConcurrentContender()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var application = Application();
        using var gate = new ManualResetEventSlim(false);

        var contenders = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() =>
            {
                gate.Wait();
                return authority.TryAcquire(application, out var ownership)
                    ? ownership
                    : null;
            }))
            .ToArray();

        gate.Set();
        var outcomes = await Task.WhenAll(contenders);
        var admitted = outcomes.Where(static ownership => ownership is not null).ToArray();

        Assert.Single(admitted);
        Assert.True(admitted[0]!.Release().Released);
    }

    [Fact]
    public void Release_ShouldDetectImpossibleNonActiveState()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var application = Application();

        Assert.True(authority.TryAcquire(application, out var ownership));
        Assert.True(ownership!.Release().Released);

        var impossibleRelease = ownership.Release();

        Assert.False(impossibleRelease.Released);
        Assert.Equal(
            ApplicationInvocationCoordinationAuthority.OccupancyCell.Idle,
            impossibleRelease.ObservedState);
    }

    [Fact]
    public void WeakAssociation_ShouldNotPermanentlyRetainApplication()
    {
        var authority = new ApplicationInvocationCoordinationAuthority();
        var weakApplication = AcquireReleaseAndForget(authority);

        ForceCollection();

        Assert.False(weakApplication.IsAlive);
        GC.KeepAlive(authority);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AcquireReleaseAndForget(
        ApplicationInvocationCoordinationAuthority authority)
    {
        var application = Application();
        Assert.True(authority.TryAcquire(application, out var ownership));
        Assert.True(ownership!.Release().Released);
        return new WeakReference(application);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ForceCollection()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }

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
}
