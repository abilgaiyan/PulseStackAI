using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Core.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphResolutionOperationBoundaryTests
{
    [Fact]
    public async Task KnownRootReentry_ShouldConvergeWithoutReferenceResolutionOrCycleSemantics()
    {
        var root = Key(AssetType.Package);
        var rootUrn = Urn("root");
        var rootAsset = new TestAsset(root, rootUrn);
        var resolver = new BoundaryResolver(rootAsset);
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();

        var relationship = new AIAssetGraphRelationship(
            root,
            new AssetReference(root.Type, root.Id, rootUrn, root.Version),
            AIAssetGraphRelationshipClass.InternalDistribution,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.Internal,
            null,
            0,
            "$.members[0]");
        var path = new AIAssetGraphPath(root, new[] { new AIAssetGraphPathSegment(relationship) });

        var result = await operation.ProcessRelationshipAsync(path, relationship);

        result.Should().BeNull();
        resolver.ExactCalls.Should().Be(1);
        resolver.ReferenceCalls.Should().Be(0);
        operation.MaterializedNodes.Should().ContainSingle();
        operation.ObservedRelationships.Should().ContainSingle().Which.Should().BeSameAs(relationship);
        operation.TerminalFailure.Should().BeNull();
    }

    [Fact]
    public async Task WorkItemPath_ShouldBeBoundToOperationRoot()
    {
        var root = Key(AssetType.Project);
        var rootAsset = new TestAsset(root, Urn("root"));
        var resolver = new BoundaryResolver(rootAsset);
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();

        var otherRoot = Key(AssetType.Package);
        var target = Key(AssetType.Prompt);
        var relationship = new AIAssetGraphRelationship(
            otherRoot,
            new AssetReference(target.Type, target.Id, Urn("target"), target.Version),
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.External,
            true,
            0,
            "$.dependencies[0]");
        var path = new AIAssetGraphPath(otherRoot, new[] { new AIAssetGraphPathSegment(relationship) });

        Func<Task> act = async () => { await operation.ProcessRelationshipAsync(path, relationship); };

        await act.Should().ThrowAsync<ArgumentException>();
        resolver.ReferenceCalls.Should().Be(0);
        operation.ObservedRelationships.Should().BeEmpty();
    }

    private static AssetDefinitionKey Key(AssetType type) =>
        new(type, AssetId.New(), new AssetVersion("1.0"));

    private static AssetUrn Urn(string suffix) =>
        new($"urn:pulsestack:test:{suffix}:{Guid.NewGuid():N}");

    private sealed record TestAsset : Asset
    {
        [SetsRequiredMembers]
        internal TestAsset(AssetDefinitionKey key, AssetUrn urn)
            : base(key.Type)
        {
            Id = key.Id;
            Urn = urn;
            Version = key.Version;
            Metadata = new AssetMetadata { Name = "test" };
            Lifecycle = AssetLifecycle.Published;
        }
    }

    private sealed class BoundaryResolver : IPersistentAIAssetResolver
    {
        private readonly IAsset rootAsset;

        internal BoundaryResolver(IAsset rootAsset) => this.rootAsset = rootAsset;

        internal int ExactCalls { get; private set; }
        internal int ReferenceCalls { get; private set; }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            ExactCalls++;
            return ValueTask.FromResult<AIAssetResolutionResult>(
                new AIAssetResolutionResult.Resolved(rootAsset));
        }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetReference reference,
            CancellationToken cancellationToken = default)
        {
            ReferenceCalls++;
            throw new InvalidOperationException("Boundary proof must not resolve a reference.");
        }

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
