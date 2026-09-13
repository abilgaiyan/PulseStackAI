using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Core.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphFailureCoordinatorTests
{
    [Fact]
    public void CanonicalRelationshipComparison_ShouldUseLocalOrdinalFirst()
    {
        var source = Key(AssetType.Package, 1);
        var target = Key(AssetType.Tool, 2);
        var later = Relationship(source, target, 2, "Members[2]");
        var earlier = Relationship(source, target, 1, "Members[9]");

        AIAssetGraphFailureComparer.CompareRelationships(earlier, later).Should().BeLessThan(0);
    }

    [Fact]
    public void CanonicalRelationshipComparison_ShouldUseNormalizedSourceKeyAfterOrdinal()
    {
        var earlierSource = Key(AssetType.Project, 1);
        var laterSource = Key(AssetType.Package, 1);
        var target = Key(AssetType.Tool, 2);
        var earlier = Relationship(earlierSource, target, 0, "Dependencies[0]");
        var later = Relationship(laterSource, target, 0, "Dependencies[0]");

        AIAssetGraphFailureComparer.CompareRelationships(earlier, later).Should().BeLessThan(0);
    }

    [Fact]
    public void CanonicalRelationshipComparison_ShouldUseNormalizedTargetKeyAfterSource()
    {
        var source = Key(AssetType.Package, 1);
        var earlierTarget = Key(AssetType.Workflow, 2);
        var laterTarget = Key(AssetType.Tool, 2);
        var earlier = Relationship(source, earlierTarget, 0, "Members[0]");
        var later = Relationship(source, laterTarget, 0, "Members[0]");

        AIAssetGraphFailureComparer.CompareRelationships(earlier, later).Should().BeLessThan(0);
    }

    [Fact]
    public void CanonicalRelationshipComparison_ShouldUseOrdinalAuthoredPathLast()
    {
        var source = Key(AssetType.Package, 1);
        var target = Key(AssetType.Tool, 2);
        var earlier = Relationship(source, target, 0, "Members[A]");
        var later = Relationship(source, target, 0, "Members[a]");

        AIAssetGraphFailureComparer.CompareRelationships(earlier, later).Should().BeLessThan(0);
    }

    [Fact]
    public void CanonicalPathComparison_ShouldPreferExactPrefix()
    {
        var root = Key(AssetType.Package, 1);
        var child = Key(AssetType.Tool, 2);
        var leaf = Key(AssetType.Prompt, 3);
        var first = Relationship(root, child, 0, "Members[0]");
        var second = Relationship(child, leaf, 0, "Dependencies[0]");
        var prefix = Path(root, first);
        var longer = Path(root, first, second);

        AIAssetGraphFailureComparer.ComparePaths(prefix, longer).Should().BeLessThan(0);
    }

    [Fact]
    public void Coordinator_ShouldSelectSmallerPathRegardlessOfObservationOrder()
    {
        var root = Key(AssetType.Package, 1);
        var earlier = RequiredUnavailable(root, Relationship(root, Key(AssetType.Tool, 2), 0, "Members[0]"));
        var later = RequiredUnavailable(root, Relationship(root, Key(AssetType.Tool, 3), 1, "Members[1]"));

        var first = new AIAssetGraphFailureCoordinator(root);
        first.Observe(later);
        first.Observe(earlier);

        var second = new AIAssetGraphFailureCoordinator(root);
        second.Observe(earlier);
        second.Observe(later);

        first.Commit().Should().BeSameAs(earlier);
        second.Commit().Should().BeSameAs(earlier);
    }

    [Fact]
    public void Coordinator_ShouldAcceptMatchingRootCandidate()
    {
        var root = Key(AssetType.Package, 1);
        var failure = RequiredUnavailable(root, Relationship(root, Key(AssetType.Tool, 2), 0, "Members[0]"));
        var coordinator = new AIAssetGraphFailureCoordinator(root);

        coordinator.Observe(failure);

        coordinator.RootKey.Should().Be(root);
        coordinator.Commit().Should().BeSameAs(failure);
    }

    [Fact]
    public void Coordinator_ShouldRejectDifferentRootBeforeAffectingCandidate()
    {
        var root = Key(AssetType.Package, 1);
        var otherRoot = Key(AssetType.Project, 2);
        var valid = RequiredUnavailable(root, Relationship(root, Key(AssetType.Tool, 3), 1, "Members[1]"));
        var foreign = RequiredUnavailable(otherRoot, Relationship(otherRoot, Key(AssetType.Workflow, 4), 0, "OwnedAssets[0]"));
        var coordinator = new AIAssetGraphFailureCoordinator(root);
        coordinator.Observe(valid);

        Action act = () => coordinator.Observe(foreign);

        act.Should().Throw<ArgumentException>();
        coordinator.Commit().Should().BeSameAs(valid);
    }

    [Fact]
    public void SeparateCoordinators_ShouldSelectFailuresIndependentlyPerRoot()
    {
        var packageRoot = Key(AssetType.Package, 1);
        var projectRoot = Key(AssetType.Project, 2);
        var packageFailure = RequiredUnavailable(
            packageRoot,
            Relationship(packageRoot, Key(AssetType.Tool, 3), 0, "Members[0]"));
        var projectFailure = RequiredUnavailable(
            projectRoot,
            Relationship(projectRoot, Key(AssetType.Workflow, 4), 0, "OwnedAssets[0]"));
        var packageCoordinator = new AIAssetGraphFailureCoordinator(packageRoot);
        var projectCoordinator = new AIAssetGraphFailureCoordinator(projectRoot);

        packageCoordinator.Observe(packageFailure);
        projectCoordinator.Observe(projectFailure);

        packageCoordinator.Commit().Should().BeSameAs(packageFailure);
        projectCoordinator.Commit().Should().BeSameAs(projectFailure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IdentityConflict_ShouldPrecedeUnavailabilityOnSamePath(bool lineage)
    {
        var root = Key(AssetType.Package, 1);
        var target = Key(AssetType.Tool, 2);
        var relationship = Relationship(root, target, 0, "Members[0]");
        var path = Path(root, relationship);
        var unavailable = RequiredUnavailable(path, relationship);
        AIAssetGraphLoadResult identity = lineage
            ? LineageConflict(path, relationship)
            : ReferenceConflict(path, relationship);
        var coordinator = new AIAssetGraphFailureCoordinator(root);

        coordinator.Observe(unavailable);
        coordinator.Observe(identity);

        coordinator.Commit().Should().BeSameAs(identity);
    }

    [Fact]
    public void Unavailability_ShouldPrecedeCycleOnSamePath()
    {
        var root = Key(AssetType.Package, 1);
        var active = Key(AssetType.Tool, 2);
        var enter = Relationship(root, active, 0, "Members[0]");
        var close = Relationship(active, active, 0, "Dependencies[0]");
        var path = Path(root, enter, close);
        var unavailable = RequiredUnavailable(path, close);
        var cycle = Cycle(path, close, active, 1);
        var coordinator = new AIAssetGraphFailureCoordinator(root);

        coordinator.Observe(cycle);
        coordinator.Observe(unavailable);

        coordinator.Commit().Should().BeSameAs(unavailable);
    }

    [Fact]
    public void SamePathPrecedence_ShouldNotOverrideSmallerDifferentPath()
    {
        var root = Key(AssetType.Package, 1);
        var earlierRelationship = Relationship(root, Key(AssetType.Tool, 2), 0, "Members[0]");
        var laterRelationship = Relationship(root, Key(AssetType.Tool, 3), 1, "Members[1]");
        var earlierUnavailable = RequiredUnavailable(root, earlierRelationship);
        var laterIdentity = ReferenceConflict(Path(root, laterRelationship), laterRelationship);
        var coordinator = new AIAssetGraphFailureCoordinator(root);

        coordinator.Observe(laterIdentity);
        coordinator.Observe(earlierUnavailable);

        coordinator.Commit().Should().BeSameAs(earlierUnavailable);
    }

    [Fact]
    public void RootAbsence_ShouldRetainAag001AndEmptyPath()
    {
        var root = Key(AssetType.Package, 1);
        var failure = new AIAssetGraphLoadResult.RootDefinitionUnavailable(
            new AIAssetGraphRootDefinitionUnavailableContext(root));
        var coordinator = new AIAssetGraphFailureCoordinator(root);

        coordinator.Observe(failure);
        var committed = coordinator.Commit();

        committed.Should().BeSameAs(failure);
        failure.Context.Code.Should().Be(AIAssetGraphDiagnosticCodes.RootDefinitionUnavailable);
        failure.Context.CanonicalPath.Segments.Should().BeEmpty();
    }

    [Fact]
    public void Commit_ShouldPublishOneTerminalSemanticResultOnly()
    {
        var root = Key(AssetType.Package, 1);
        var first = RequiredUnavailable(root, Relationship(root, Key(AssetType.Tool, 2), 1, "Members[1]"));
        var wouldHaveWonBeforeCommit = RequiredUnavailable(root, Relationship(root, Key(AssetType.Tool, 3), 0, "Members[0]"));
        var coordinator = new AIAssetGraphFailureCoordinator(root);
        coordinator.Observe(first);

        coordinator.Commit().Should().BeSameAs(first);
        coordinator.Observe(wouldHaveWonBeforeCommit);

        coordinator.Commit().Should().BeSameAs(first);
        coordinator.CommittedFailure.Should().BeSameAs(first);
    }

    [Fact]
    public async Task PredecessorException_ShouldRemainUnwrappedWithoutCommittedGraphFailure()
    {
        var root = Key(AssetType.Package, 1);
        var coordinator = new AIAssetGraphFailureCoordinator(root);
        var predecessor = new InvalidOperationException("predecessor-authority");

        Func<Task> act = () => Task.Run(() => coordinator.PreservePredecessorFailure(predecessor));

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Should().BeSameAs(predecessor);
    }

    [Fact]
    public void CommittedGraphFailure_ShouldDominateLaterPredecessorFailure()
    {
        var root = Key(AssetType.Package, 1);
        var failure = RequiredUnavailable(root, Relationship(root, Key(AssetType.Tool, 2), 0, "Members[0]"));
        var coordinator = new AIAssetGraphFailureCoordinator(root);
        coordinator.Observe(failure);
        coordinator.Commit();

        var result = coordinator.PreservePredecessorFailure(new InvalidOperationException("late"));

        result.Should().BeSameAs(failure);
    }

    [Fact]
    public void CallerCancellationBeforeCommit_ShouldWinAndPreserveCallerToken()
    {
        var root = Key(AssetType.Package, 1);
        var failure = RequiredUnavailable(root, Relationship(root, Key(AssetType.Tool, 2), 0, "Members[0]"));
        var coordinator = new AIAssetGraphFailureCoordinator(root);
        coordinator.Observe(failure);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Action act = () => coordinator.Commit(cts.Token);

        var thrown = act.Should().Throw<OperationCanceledException>().Which;
        thrown.CancellationToken.Should().Be(cts.Token);
        coordinator.CommittedFailure.Should().BeNull();
    }

    [Fact]
    public void CallerCancellationAfterCommit_ShouldNotRewriteSemanticResult()
    {
        var root = Key(AssetType.Package, 1);
        var failure = RequiredUnavailable(root, Relationship(root, Key(AssetType.Tool, 2), 0, "Members[0]"));
        var coordinator = new AIAssetGraphFailureCoordinator(root);
        coordinator.Observe(failure);
        coordinator.Commit();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        coordinator.Commit(cts.Token).Should().BeSameAs(failure);
    }

    [Fact]
    public async Task CompletionTiming_ShouldNotSelectTheLaterCanonicalPath()
    {
        var root = Key(AssetType.Package, 1);
        var earlier = RequiredUnavailable(root, Relationship(root, Key(AssetType.Tool, 2), 0, "Members[0]"));
        var later = RequiredUnavailable(root, Relationship(root, Key(AssetType.Tool, 3), 1, "Members[1]"));
        var coordinator = new AIAssetGraphFailureCoordinator(root);

        var slowEarlier = Task.Run(async () =>
        {
            await Task.Delay(30);
            coordinator.Observe(earlier);
        });
        var fastLater = Task.Run(async () =>
        {
            await Task.Delay(1);
            coordinator.Observe(later);
        });

        await Task.WhenAll(slowEarlier, fastLater);

        coordinator.Commit().Should().BeSameAs(earlier);
    }

    [Fact]
    public void CycleSelection_ShouldRetainClosingBackEdgeAndCycleContext()
    {
        var root = Key(AssetType.Package, 1);
        var active = Key(AssetType.Tool, 2);
        var enter = Relationship(root, active, 0, "Members[0]");
        var close = Relationship(active, active, 0, "Dependencies[0]");
        var path = Path(root, enter, close);
        var cycle = Cycle(path, close, active, 1);
        var coordinator = new AIAssetGraphFailureCoordinator(root);

        coordinator.Observe(cycle);
        var committed = coordinator.Commit().Should().BeOfType<AIAssetGraphLoadResult.RequiredMaterializationCycle>().Subject;

        committed.Context.CanonicalPath.Segments[^1].Relationship.Should().BeSameAs(close);
        committed.Context.CycleEntryKey.Should().Be(active);
        committed.Context.CycleStartSegmentIndex.Should().Be(1);
    }

    [Fact]
    public void SemanticFailureCarrier_ShouldExposeNoPartialGraphSurface()
    {
        var failureTypes = new[]
        {
            typeof(AIAssetGraphLoadResult.RootDefinitionUnavailable),
            typeof(AIAssetGraphLoadResult.RequiredDefinitionUnavailable),
            typeof(AIAssetGraphLoadResult.ReferenceIdentityConflict),
            typeof(AIAssetGraphLoadResult.LineageIdentityConflict),
            typeof(AIAssetGraphLoadResult.RequiredMaterializationCycle)
        };

        failureTypes.Should().OnlyContain(type => type.GetProperty("Graph") == null);
        typeof(AIAssetGraphFailureContext).GetProperties()
            .Should().NotContain(property => typeof(IAsset).IsAssignableFrom(property.PropertyType));
    }

    private static AIAssetGraphLoadResult.RequiredDefinitionUnavailable RequiredUnavailable(
        AssetDefinitionKey root,
        AIAssetGraphRelationship relationship) =>
        RequiredUnavailable(Path(root, relationship), relationship);

    private static AIAssetGraphLoadResult.RequiredDefinitionUnavailable RequiredUnavailable(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship) =>
        new(new AIAssetGraphRequiredDefinitionUnavailableContext(path.RootKey, path, relationship));

    private static AIAssetGraphLoadResult.ReferenceIdentityConflict ReferenceConflict(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship) =>
        new(new AIAssetGraphReferenceIdentityConflictContext(
            path.RootKey,
            path,
            relationship,
            AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity));

    private static AIAssetGraphLoadResult.LineageIdentityConflict LineageConflict(
        AIAssetGraphPath path,
        AIAssetGraphRelationship relationship)
    {
        var conflicting = AssetDefinitionKey.From(relationship.TargetReference);
        var established = Key(
            conflicting.Type == AssetType.Tool ? AssetType.Prompt : AssetType.Tool,
            90);
        return new AIAssetGraphLoadResult.LineageIdentityConflict(
            new AIAssetGraphLineageIdentityConflictContext(
                path.RootKey,
                path,
                relationship,
                relationship.TargetReference.Urn,
                established,
                conflicting));
    }

    private static AIAssetGraphLoadResult.RequiredMaterializationCycle Cycle(
        AIAssetGraphPath path,
        AIAssetGraphRelationship closingRelationship,
        AssetDefinitionKey entry,
        int startIndex) =>
        new(new AIAssetGraphRequiredMaterializationCycleContext(
            path.RootKey,
            path,
            closingRelationship,
            entry,
            startIndex));

    private static AIAssetGraphPath Path(
        AssetDefinitionKey root,
        params AIAssetGraphRelationship[] relationships) =>
        new(root, relationships.Select(static relationship => new AIAssetGraphPathSegment(relationship)));

    private static AIAssetGraphRelationship Relationship(
        AssetDefinitionKey source,
        AssetDefinitionKey target,
        int ordinal,
        string authoredPath) =>
        new(
            source,
            new AssetReference(target.Type, target.Id, Urn(target), target.Version),
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.External,
            true,
            ordinal,
            authoredPath);

    private static AssetDefinitionKey Key(AssetType type, int value) =>
        new(
            type,
            new AssetId(Guid.Parse($"00000000-0000-0000-0000-{value:D12}")),
            AssetVersion.Initial);

    private static AssetUrn Urn(AssetDefinitionKey key) =>
        new($"urn:pulsestack:test:b5:{key.Type}:{key.Id.Value:D}");
}
