using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Core.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphResolutionOperationRepairTests
{
    [Fact]
    public async Task ConcurrentRequiredConvergence_ShouldInvokeResolverOnceAndShareMaterializedOutcome()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var target = Key(AssetType.Prompt);
        var targetUrn = Urn("target");
        var targetAsset = Asset(target, targetUrn);
        var resolution = new TaskCompletionSource<AIAssetResolutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var referenceCalls = 0;

        var resolver = new AsyncScriptedResolver(
            key => ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.Resolved(rootAsset)),
            _ =>
            {
                Interlocked.Increment(ref referenceCalls);
                return new ValueTask<AIAssetResolutionResult>(resolution.Task);
            });

        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        (await operation.ResolveRootAsync()).Should().BeNull();

        var first = Required(root, Reference(target, targetUrn), 0, "$.members[0]");
        var second = Required(root, Reference(target, targetUrn), 1, "$.members[1]");

        var firstTask = operation.ProcessRelationshipAsync(Path(root, first), first).AsTask();
        var secondTask = operation.ProcessRelationshipAsync(Path(root, second), second).AsTask();

        Volatile.Read(ref referenceCalls).Should().Be(1);
        resolution.SetResult(new AIAssetResolutionResult.Resolved(targetAsset));

        var outcomes = await Task.WhenAll(firstTask, secondTask);

        outcomes.Should().OnlyContain(static outcome => outcome == null);
        Volatile.Read(ref referenceCalls).Should().Be(1);
        operation.MaterializedNodes.Count(node => node.DefinitionKey == target).Should().Be(1);
        operation.ObservedRelationships.Should().Equal(first, second);
    }

    [Fact]
    public async Task ConcurrentRequiredFailure_ShouldInvokeResolverOnceAndReuseSameTerminalFailure()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var target = Key(AssetType.Model);
        var targetUrn = Urn("missing");
        var resolution = new TaskCompletionSource<AIAssetResolutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var referenceCalls = 0;

        var resolver = new AsyncScriptedResolver(
            _ => ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.Resolved(rootAsset)),
            _ =>
            {
                Interlocked.Increment(ref referenceCalls);
                return new ValueTask<AIAssetResolutionResult>(resolution.Task);
            });

        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();
        var first = Required(root, Reference(target, targetUrn), 0, "$.dependencies[0]");
        var second = Required(root, Reference(target, targetUrn), 1, "$.dependencies[1]");

        var firstTask = operation.ProcessRelationshipAsync(Path(root, first), first).AsTask();
        var secondTask = operation.ProcessRelationshipAsync(Path(root, second), second).AsTask();
        Volatile.Read(ref referenceCalls).Should().Be(1);

        resolution.SetResult(new AIAssetResolutionResult.DefinitionNotPublished());
        var outcomes = await Task.WhenAll(firstTask, secondTask);

        Volatile.Read(ref referenceCalls).Should().Be(1);
        outcomes[0].Should().BeSameAs(outcomes[1]);
        outcomes[0].Should().BeOfType<AIAssetGraphLoadResult.RequiredDefinitionUnavailable>();
        operation.MaterializedNodes.Should().NotContain(node => node.DefinitionKey == target);
    }

    [Fact]
    public async Task OptionalReferences_ShouldEstablishLineageWithoutResolving()
    {
        var root = Key(AssetType.Library);
        var rootAsset = Asset(root, Urn("root"));
        var sharedUrn = Urn("shared");
        var firstTarget = Key(AssetType.Prompt);
        var secondTarget = Key(AssetType.Tool);
        var referenceCalls = 0;
        var resolver = new AsyncScriptedResolver(
            _ => ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.Resolved(rootAsset)),
            _ =>
            {
                Interlocked.Increment(ref referenceCalls);
                throw new InvalidOperationException("Optional relationships must not invoke the resolver.");
            });
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();

        var first = Optional(root, Reference(firstTarget, sharedUrn), 0, "$.dependencies[0]");
        var second = Optional(root, Reference(secondTarget, sharedUrn), 1, "$.dependencies[1]");

        (await operation.ProcessRelationshipAsync(Path(root, first), first)).Should().BeNull();
        var failure = await operation.ProcessRelationshipAsync(Path(root, second), second);

        Volatile.Read(ref referenceCalls).Should().Be(0);
        var conflict = failure.Should().BeOfType<AIAssetGraphLoadResult.LineageIdentityConflict>().Subject;
        conflict.Context.EstablishedIdentity.Should().Be(firstTarget);
        conflict.Context.ConflictingIdentity.Should().Be(secondTarget);
        conflict.Context.ConflictingUrn.Should().Be(sharedUrn);
        conflict.Context.MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Excluded);
    }

    [Fact]
    public async Task OptionalSameKeyDifferentUrn_ShouldProduceOperationLocalAag003WithoutResolving()
    {
        var root = Key(AssetType.Project);
        var rootAsset = Asset(root, Urn("root"));
        var target = Key(AssetType.Knowledge);
        var referenceCalls = 0;
        var resolver = new AsyncScriptedResolver(
            _ => ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.Resolved(rootAsset)),
            _ =>
            {
                Interlocked.Increment(ref referenceCalls);
                throw new InvalidOperationException("Optional relationships must not invoke the resolver.");
            });
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();

        var first = Optional(root, Reference(target, Urn("first")), 0, "$.dependencies[0]");
        var second = Optional(root, Reference(target, Urn("second")), 1, "$.dependencies[1]");

        (await operation.ProcessRelationshipAsync(Path(root, first), first)).Should().BeNull();
        var failure = await operation.ProcessRelationshipAsync(Path(root, second), second);

        Volatile.Read(ref referenceCalls).Should().Be(0);
        var conflict = failure.Should().BeOfType<AIAssetGraphLoadResult.ReferenceIdentityConflict>().Subject;
        conflict.Context.Evidence.Should().Be(AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity);
        conflict.Context.MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Excluded);
    }

    [Fact]
    public async Task RelationshipFromUnmaterializedOptionalTarget_ShouldBeRejectedBeforeTraversal()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var missingSource = Key(AssetType.Library);
        var target = Key(AssetType.Prompt);
        var referenceCalls = 0;
        var resolver = new AsyncScriptedResolver(
            _ => ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.Resolved(rootAsset)),
            _ =>
            {
                Interlocked.Increment(ref referenceCalls);
                return ValueTask.FromResult<AIAssetResolutionResult>(
                    new AIAssetResolutionResult.Resolved(Asset(target, Urn("unexpected"))));
            });
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();

        var optionalSource = Optional(root, Reference(missingSource, Urn("missing-source")), 0, "$.dependencies[0]");
        (await operation.ProcessRelationshipAsync(Path(root, optionalSource), optionalSource)).Should().BeNull();

        var child = Required(missingSource, Reference(target, Urn("target")), 0, "$.members[0]");
        var childPath = new AIAssetGraphPath(
            root,
            new[]
            {
                new AIAssetGraphPathSegment(optionalSource),
                new AIAssetGraphPathSegment(child)
            });

        Func<Task> act = async () => await operation.ProcessRelationshipAsync(childPath, child);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*successfully materialized source node*");
        Volatile.Read(ref referenceCalls).Should().Be(0);
        operation.ObservedRelationships.Should().ContainSingle().Which.Should().BeSameAs(optionalSource);
    }

    [Fact]
    public async Task RelationshipBeforeRootResolution_ShouldBeRejected()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var resolver = new AsyncScriptedResolver(
            _ => ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.Resolved(rootAsset)),
            _ => throw new InvalidOperationException("Relationship must be rejected before resolution."));
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        var relationship = Required(root, Reference(Key(AssetType.Prompt), Urn("target")), 0, "$.members[0]");

        Func<Task> act = async () => await operation.ProcessRelationshipAsync(Path(root, relationship), relationship);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*successfully materialized source node*");
        operation.ObservedRelationships.Should().BeEmpty();
    }

    private static AIAssetGraphRelationship Required(
        AssetDefinitionKey source,
        AssetReference target,
        int ordinal,
        string path) =>
        new(
            source,
            target,
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.External,
            true,
            ordinal,
            path);

    private static AIAssetGraphRelationship Optional(
        AssetDefinitionKey source,
        AssetReference target,
        int ordinal,
        string path) =>
        new(
            source,
            target,
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphMaterializationAuthority.Excluded,
            AIAssetGraphBoundaryRole.External,
            false,
            ordinal,
            path);

    private static AIAssetGraphPath Path(AssetDefinitionKey root, AIAssetGraphRelationship relationship) =>
        new(root, new[] { new AIAssetGraphPathSegment(relationship) });

    private static AssetDefinitionKey Key(AssetType type) =>
        new(type, AssetId.New(), new AssetVersion("1.0"));

    private static AssetReference Reference(AssetDefinitionKey key, AssetUrn urn) =>
        new(key.Type, key.Id, urn, key.Version);

    private static AssetUrn Urn(string suffix) =>
        new($"urn:pulsestack:test:{suffix}:{Guid.NewGuid():N}");

    private static IAsset Asset(AssetDefinitionKey key, AssetUrn urn) =>
        new TestAsset(key, urn);

    private sealed record TestAsset : Asset
    {
        [SetsRequiredMembers]
        public TestAsset(AssetDefinitionKey key, AssetUrn urn)
            : base(key.Type)
        {
            Id = key.Id;
            Urn = urn;
            Version = key.Version;
            Metadata = new AssetMetadata { Name = "test" };
            Lifecycle = AssetLifecycle.Published;
        }
    }

    private sealed class AsyncScriptedResolver : IPersistentAIAssetResolver
    {
        private readonly Func<AssetDefinitionKey, ValueTask<AIAssetResolutionResult>> exact;
        private readonly Func<AssetReference, ValueTask<AIAssetResolutionResult>> reference;

        internal AsyncScriptedResolver(
            Func<AssetDefinitionKey, ValueTask<AIAssetResolutionResult>> exact,
            Func<AssetReference, ValueTask<AIAssetResolutionResult>> reference)
        {
            this.exact = exact;
            this.reference = reference;
        }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default) => exact(key);

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetReference reference,
            CancellationToken cancellationToken = default) => this.reference(reference);

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetUrn urn,
            AssetVersion version,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<CatalogLineageLookupResult> DiscoverLineageAsync(
            AssetUrn urn,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
