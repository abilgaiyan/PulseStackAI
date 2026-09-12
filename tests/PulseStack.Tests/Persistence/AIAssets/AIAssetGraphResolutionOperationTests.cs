using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Core.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphResolutionOperationTests
{
    [Fact]
    public async Task Root_ShouldUseExactKeyResolutionOnce_AndMaterializeExactIdentity()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var resolver = new ScriptedResolver
        {
            ExactHandler = _ => new AIAssetResolutionResult.Resolved(rootAsset)
        };
        var operation = new AIAssetGraphResolutionOperation(resolver, root);

        var first = await operation.ResolveRootAsync();
        var second = await operation.ResolveRootAsync();

        first.Should().BeNull();
        second.Should().BeNull();
        resolver.ExactCalls[root].Should().Be(1);
        resolver.ReferenceCalls.Should().BeEmpty();
        operation.MaterializedNodes.Should().ContainSingle()
            .Which.DefinitionKey.Should().Be(root);
    }

    [Fact]
    public async Task RequiredRelationship_ShouldUseExactAuthoredReference_AndOptionalShouldNeverResolve()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var requiredReference = Reference(Key(AssetType.Prompt), Urn("required"));
        var optionalReference = Reference(Key(AssetType.Tool), Urn("optional"));
        var requiredAsset = Asset(AssetDefinitionKey.From(requiredReference), requiredReference.Urn);
        var resolver = ResolverForRoot(root, rootAsset);
        resolver.ReferenceHandler = reference =>
            AssetDefinitionKey.From(reference) == AssetDefinitionKey.From(requiredReference)
                ? new AIAssetResolutionResult.Resolved(requiredAsset)
                : throw new InvalidOperationException("Optional relationship must not invoke the resolver.");
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();

        var optional = ExplicitRequirement(root, optionalReference, required: false, ordinal: 0, "$.dependencies[0]");
        var required = ExplicitRequirement(root, requiredReference, required: true, ordinal: 1, "$.dependencies[1]");

        (await operation.ProcessRelationshipAsync(Path(root, optional), optional)).Should().BeNull();
        (await operation.ProcessRelationshipAsync(Path(root, required), required)).Should().BeNull();

        resolver.ReferenceCalls.Should().ContainSingle();
        resolver.ReferenceCalls[AssetDefinitionKey.From(requiredReference)].Should().Be(1);
        resolver.ReferenceCalls.Should().NotContainKey(AssetDefinitionKey.From(optionalReference));
        operation.ObservedRelationships.Should().Equal(optional, required);
    }

    [Fact]
    public async Task RequiredAndRequired_ShouldConvergeOnOneNode_WithoutDeduplicatingRelationships()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var targetKey = Key(AssetType.Prompt);
        var targetUrn = Urn("target");
        var firstReference = Reference(targetKey, targetUrn);
        var secondReference = Reference(targetKey, targetUrn);
        var targetAsset = Asset(targetKey, targetUrn);
        var resolver = ResolverForRoot(root, rootAsset);
        resolver.ReferenceHandler = _ => new AIAssetResolutionResult.Resolved(targetAsset);
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();

        var first = new AIAssetGraphRelationship(
            root,
            firstReference,
            AIAssetGraphRelationshipClass.InternalDistribution,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.Internal,
            null,
            0,
            "$.members[0]");
        var second = ExplicitRequirement(root, secondReference, required: true, ordinal: 1, "$.dependencies[0]");

        (await operation.ProcessRelationshipAsync(Path(root, first), first)).Should().BeNull();
        (await operation.ProcessRelationshipAsync(Path(root, second), second)).Should().BeNull();

        resolver.ReferenceCalls[targetKey].Should().Be(1);
        operation.MaterializedNodes.Count(node => node.DefinitionKey == targetKey).Should().Be(1);
        operation.ObservedRelationships.Should().Equal(first, second);
        operation.ObservedRelationships[0].RelationshipClass.Should().Be(AIAssetGraphRelationshipClass.InternalDistribution);
        operation.ObservedRelationships[1].RelationshipClass.Should().Be(AIAssetGraphRelationshipClass.ExplicitRequirement);
    }

    [Fact]
    public async Task OptionalBeforeRequired_ShouldConvergeWhenLaterRequiredPathMaterializesSameIdentity()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var targetKey = Key(AssetType.Tool);
        var targetUrn = Urn("shared");
        var optionalReference = Reference(targetKey, targetUrn);
        var requiredReference = Reference(targetKey, targetUrn);
        var targetAsset = Asset(targetKey, targetUrn);
        var resolver = ResolverForRoot(root, rootAsset);
        resolver.ReferenceHandler = _ => new AIAssetResolutionResult.Resolved(targetAsset);
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();

        var optional = ExplicitRequirement(root, optionalReference, required: false, ordinal: 0, "$.dependencies[0]");
        var required = ExplicitRequirement(root, requiredReference, required: true, ordinal: 1, "$.dependencies[1]");

        (await operation.ProcessRelationshipAsync(Path(root, optional), optional)).Should().BeNull();
        resolver.ReferenceCalls.Should().BeEmpty();
        (await operation.ProcessRelationshipAsync(Path(root, required), required)).Should().BeNull();

        resolver.ReferenceCalls[targetKey].Should().Be(1);
        operation.MaterializedNodes.Count(node => node.DefinitionKey == targetKey).Should().Be(1);
        operation.ObservedRelationships.Should().Equal(optional, required);
        operation.ObservedRelationships[0].MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Excluded);
    }

    [Fact]
    public async Task KnownKeyWithDifferentUrn_ShouldProduceOperationLocalAag003_WithoutSecondResolution()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var targetKey = Key(AssetType.Tool);
        var resolvedUrn = Urn("resolved");
        var resolvedReference = Reference(targetKey, resolvedUrn);
        var conflictingReference = Reference(targetKey, Urn("conflicting"));
        var targetAsset = Asset(targetKey, resolvedUrn);
        var resolver = ResolverForRoot(root, rootAsset);
        resolver.ReferenceHandler = _ => new AIAssetResolutionResult.Resolved(targetAsset);
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();

        var required = ExplicitRequirement(root, resolvedReference, required: true, ordinal: 0, "$.dependencies[0]");
        var optionalConflict = ExplicitRequirement(root, conflictingReference, required: false, ordinal: 1, "$.dependencies[1]");

        (await operation.ProcessRelationshipAsync(Path(root, required), required)).Should().BeNull();
        var failure = await operation.ProcessRelationshipAsync(Path(root, optionalConflict), optionalConflict);

        resolver.ReferenceCalls[targetKey].Should().Be(1);
        var conflict = failure.Should().BeOfType<AIAssetGraphLoadResult.ReferenceIdentityConflict>().Subject;
        conflict.Context.Code.Should().Be(AIAssetGraphDiagnosticCodes.ReferenceIdentityConflict);
        conflict.Context.Evidence.Should().Be(AIAssetGraphReferenceIdentityConflictEvidence.OperationLocalIdentity);
        conflict.Context.PredecessorSemanticOutcome.Should().BeNull();
        conflict.Context.MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Excluded);
        conflict.Context.Relationship.Should().BeSameAs(optionalConflict);
    }

    [Fact]
    public async Task ResolverReferenceMismatch_ShouldTranslateToPersistentResolverAag003()
    {
        var root = Key(AssetType.Project);
        var rootAsset = Asset(root, Urn("root"));
        var reference = Reference(Key(AssetType.Prompt), Urn("prompt"));
        var resolver = ResolverForRoot(root, rootAsset);
        resolver.ReferenceHandler = _ => new AIAssetResolutionResult.ReferenceMismatch();
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();
        var relationship = ExplicitRequirement(root, reference, true, 0, "$.dependencies[0]");

        var failure = await operation.ProcessRelationshipAsync(Path(root, relationship), relationship);

        var conflict = failure.Should().BeOfType<AIAssetGraphLoadResult.ReferenceIdentityConflict>().Subject;
        conflict.Context.Evidence.Should().Be(AIAssetGraphReferenceIdentityConflictEvidence.PersistentResolver);
        conflict.Context.PredecessorSemanticOutcome.Should().Be(AIAssetGraphPredecessorSemanticOutcome.ReferenceMismatch);
        resolver.ReferenceCalls[AssetDefinitionKey.From(reference)].Should().Be(1);
    }

    [Fact]
    public async Task RootAndDescendantAbsence_ShouldRemainDistinctGraphFailures()
    {
        var missingRoot = Key(AssetType.Library);
        var missingResolver = new ScriptedResolver
        {
            ExactHandler = _ => new AIAssetResolutionResult.DefinitionNotPublished()
        };
        var missingOperation = new AIAssetGraphResolutionOperation(missingResolver, missingRoot);

        var rootFailure = await missingOperation.ResolveRootAsync();
        rootFailure.Should().BeOfType<AIAssetGraphLoadResult.RootDefinitionUnavailable>();

        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var childReference = Reference(Key(AssetType.Model), Urn("child"));
        var resolver = ResolverForRoot(root, rootAsset);
        resolver.ReferenceHandler = _ => new AIAssetResolutionResult.DefinitionNotPublished();
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();
        var relationship = ExplicitRequirement(root, childReference, true, 0, "$.dependencies[0]");

        var childFailure = await operation.ProcessRelationshipAsync(Path(root, relationship), relationship);

        childFailure.Should().BeOfType<AIAssetGraphLoadResult.RequiredDefinitionUnavailable>();
    }

    [Fact]
    public async Task TerminalFailure_ShouldBeReusedWithoutRetry_AndCannotBeRewrittenByLaterCancellation()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var targetKey = Key(AssetType.Model);
        var reference = Reference(targetKey, Urn("missing"));
        var resolver = ResolverForRoot(root, rootAsset);
        resolver.ReferenceHandler = _ => new AIAssetResolutionResult.DefinitionNotPublished();
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();
        var firstRelationship = ExplicitRequirement(root, reference, true, 0, "$.dependencies[0]");
        var secondRelationship = ExplicitRequirement(root, Reference(targetKey, reference.Urn), true, 1, "$.dependencies[1]");

        var first = await operation.ProcessRelationshipAsync(Path(root, firstRelationship), firstRelationship);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var second = await operation.ProcessRelationshipAsync(
            Path(root, secondRelationship),
            secondRelationship,
            cancelled.Token);

        second.Should().BeSameAs(first);
        resolver.ReferenceCalls[targetKey].Should().Be(1);
        operation.MaterializedNodes.Should().NotContain(node => node.DefinitionKey == targetKey);
    }

    [Fact]
    public async Task EstablishedUrnWithIncompatibleIdentity_ShouldProduceAag004WithoutResolvingConflictingKey()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var establishedKey = Key(AssetType.Prompt);
        var conflictingKey = Key(AssetType.Tool);
        var sharedUrn = Urn("shared-lineage");
        var establishedReference = Reference(establishedKey, sharedUrn);
        var conflictingReference = Reference(conflictingKey, sharedUrn);
        var establishedAsset = Asset(establishedKey, sharedUrn);
        var resolver = ResolverForRoot(root, rootAsset);
        resolver.ReferenceHandler = reference =>
            AssetDefinitionKey.From(reference) == establishedKey
                ? new AIAssetResolutionResult.Resolved(establishedAsset)
                : throw new InvalidOperationException("Conflicting lineage must be detected before resolution.");
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();
        var first = ExplicitRequirement(root, establishedReference, true, 0, "$.dependencies[0]");
        var second = ExplicitRequirement(root, conflictingReference, true, 1, "$.dependencies[1]");

        (await operation.ProcessRelationshipAsync(Path(root, first), first)).Should().BeNull();
        var failure = await operation.ProcessRelationshipAsync(Path(root, second), second);

        var conflict = failure.Should().BeOfType<AIAssetGraphLoadResult.LineageIdentityConflict>().Subject;
        conflict.Context.EstablishedIdentity.Should().Be(establishedKey);
        conflict.Context.ConflictingIdentity.Should().Be(conflictingKey);
        conflict.Context.ConflictingUrn.Should().Be(sharedUrn);
        resolver.ReferenceCalls.Should().ContainSingle();
        resolver.ReferenceCalls.Should().NotContainKey(conflictingKey);
    }

    [Fact]
    public async Task ResolutionState_ShouldBeIsolatedPerOperation()
    {
        var root = Key(AssetType.Project);
        var rootAsset = Asset(root, Urn("root"));
        var resolver = ResolverForRoot(root, rootAsset);
        var first = new AIAssetGraphResolutionOperation(resolver, root);
        var second = new AIAssetGraphResolutionOperation(resolver, root);

        await first.ResolveRootAsync();
        await first.ResolveRootAsync();
        await second.ResolveRootAsync();

        resolver.ExactCalls[root].Should().Be(2);
        first.MaterializedNodes.Should().ContainSingle();
        second.MaterializedNodes.Should().ContainSingle();
    }

    [Fact]
    public async Task CallerCancellation_ShouldBeForwardedUnchanged_AndObservedBeforeResolutionWhenAlreadyCancelled()
    {
        var root = Key(AssetType.Library);
        var rootAsset = Asset(root, Urn("root"));
        var resolver = ResolverForRoot(root, rootAsset);
        using var forwardedSource = new CancellationTokenSource();
        var operation = new AIAssetGraphResolutionOperation(resolver, root);

        await operation.ResolveRootAsync(forwardedSource.Token);

        resolver.ExactTokens.Should().ContainSingle()
            .Which.Should().Be(forwardedSource.Token);

        var secondRoot = Key(AssetType.Package);
        var secondResolver = ResolverForRoot(secondRoot, Asset(secondRoot, Urn("second-root")));
        var cancelledOperation = new AIAssetGraphResolutionOperation(secondResolver, secondRoot);
        using var cancelledSource = new CancellationTokenSource();
        cancelledSource.Cancel();

        Func<Task> act = async () => { await cancelledOperation.ResolveRootAsync(cancelledSource.Token); };

        var exception = await act.Should().ThrowAsync<OperationCanceledException>();
        exception.Which.CancellationToken.Should().Be(cancelledSource.Token);
        secondResolver.ExactCalls.Should().BeEmpty();
    }

    [Fact]
    public void Operation_ShouldDependOnPersistentResolverOnly_AndExposeNoLowerPersistenceDependency()
    {
        var type = typeof(AIAssetGraphResolutionOperation);
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single();

        constructor.GetParameters().Select(static parameter => parameter.ParameterType)
            .Should().Equal(typeof(IPersistentAIAssetResolver), typeof(AssetDefinitionKey));

        var forbiddenNames = new[]
        {
            "IAIAssetCatalogProvider",
            "IAIAssetLoader",
            "IAIAssetStore",
            "IAIAssetCodec",
            "IAIAssetDocumentValidator",
            "IAIAssetDocumentMapper"
        };

        type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(static field => field.FieldType.Name)
            .Should().NotContain(name => forbiddenNames.Contains(name, StringComparer.Ordinal));
    }

    private static ScriptedResolver ResolverForRoot(AssetDefinitionKey root, IAsset rootAsset) =>
        new()
        {
            ExactHandler = key => key == root
                ? new AIAssetResolutionResult.Resolved(rootAsset)
                : throw new InvalidOperationException($"Unexpected exact-key resolution: {key}.")
        };

    private static AIAssetGraphRelationship ExplicitRequirement(
        AssetDefinitionKey source,
        AssetReference target,
        bool required,
        int ordinal,
        string path) =>
        new(
            source,
            target,
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            required
                ? AIAssetGraphMaterializationAuthority.Required
                : AIAssetGraphMaterializationAuthority.Excluded,
            AIAssetGraphBoundaryRole.External,
            required,
            ordinal,
            path);

    private static AIAssetGraphPath Path(
        AssetDefinitionKey root,
        AIAssetGraphRelationship relationship) =>
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

    private sealed class ScriptedResolver : IPersistentAIAssetResolver
    {
        internal Func<AssetDefinitionKey, AIAssetResolutionResult>? ExactHandler { get; init; }
        internal Func<AssetReference, AIAssetResolutionResult>? ReferenceHandler { get; set; }

        internal Dictionary<AssetDefinitionKey, int> ExactCalls { get; } = [];
        internal Dictionary<AssetDefinitionKey, int> ReferenceCalls { get; } = [];
        internal List<CancellationToken> ExactTokens { get; } = [];
        internal List<CancellationToken> ReferenceTokens { get; } = [];

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            Increment(ExactCalls, key);
            ExactTokens.Add(cancellationToken);
            return ValueTask.FromResult(
                ExactHandler?.Invoke(key)
                ?? throw new InvalidOperationException("No exact-key resolver script configured."));
        }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetReference reference,
            CancellationToken cancellationToken = default)
        {
            var key = AssetDefinitionKey.From(reference);
            Increment(ReferenceCalls, key);
            ReferenceTokens.Add(cancellationToken);
            return ValueTask.FromResult(
                ReferenceHandler?.Invoke(reference)
                ?? throw new InvalidOperationException("No exact-reference resolver script configured."));
        }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetUrn urn,
            AssetVersion version,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("URN/version resolution is outside B.3 authority.");

        public ValueTask<CatalogLineageLookupResult> DiscoverLineageAsync(
            AssetUrn urn,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Lineage discovery is outside B.3 authority.");

        private static void Increment(
            Dictionary<AssetDefinitionKey, int> calls,
            AssetDefinitionKey key)
        {
            calls.TryGetValue(key, out var count);
            calls[key] = count + 1;
        }
    }
}
