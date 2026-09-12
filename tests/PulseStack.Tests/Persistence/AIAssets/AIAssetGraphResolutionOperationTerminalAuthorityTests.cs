using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Core.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphResolutionOperationTerminalAuthorityTests
{
    [Fact]
    public async Task CommittedAag003_ShouldDominateLaterResolverException()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var target = Key(AssetType.Prompt);
        var firstUrn = Urn("first");
        var secondUrn = Urn("second");
        var completion = new TaskCompletionSource<AIAssetResolutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = Resolver(rootAsset, _ => new ValueTask<AIAssetResolutionResult>(completion.Task));
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();

        var first = Required(root, Reference(target, firstUrn), 0);
        var second = Required(root, Reference(target, secondUrn), 1);
        var firstTask = operation.ProcessRelationshipAsync(Path(root, first), first).AsTask();

        var committed = await operation.ProcessRelationshipAsync(Path(root, second), second);
        committed.Should().BeOfType<AIAssetGraphLoadResult.ReferenceIdentityConflict>();

        completion.SetException(new InvalidOperationException("late resolver failure"));
        var firstOutcome = await firstTask;

        firstOutcome.Should().BeSameAs(committed);
        operation.TerminalFailure.Should().BeSameAs(committed);
    }

    [Fact]
    public async Task CommittedAag003_ShouldDominateLaterCallerCancellation()
    {
        var root = Key(AssetType.Package);
        var rootAsset = Asset(root, Urn("root"));
        var target = Key(AssetType.Tool);
        var firstUrn = Urn("first");
        var secondUrn = Urn("second");
        var targetAsset = Asset(target, firstUrn);
        var completion = new TaskCompletionSource<AIAssetResolutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = Resolver(rootAsset, _ => new ValueTask<AIAssetResolutionResult>(completion.Task));
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();
        using var cancellation = new CancellationTokenSource();

        var first = Required(root, Reference(target, firstUrn), 0);
        var second = Required(root, Reference(target, secondUrn), 1);
        var firstTask = operation.ProcessRelationshipAsync(Path(root, first), first, cancellation.Token).AsTask();

        var committed = await operation.ProcessRelationshipAsync(Path(root, second), second);
        committed.Should().BeOfType<AIAssetGraphLoadResult.ReferenceIdentityConflict>();

        cancellation.Cancel();
        completion.SetResult(new AIAssetResolutionResult.Resolved(targetAsset));
        var firstOutcome = await firstTask;

        firstOutcome.Should().BeSameAs(committed);
        operation.TerminalFailure.Should().BeSameAs(committed);
    }

    [Fact]
    public async Task ResolverExceptionWithoutPriorTerminalCommitment_ShouldRemainExceptional()
    {
        var root = Key(AssetType.Project);
        var rootAsset = Asset(root, Urn("root"));
        var target = Key(AssetType.Model);
        var completion = new TaskCompletionSource<AIAssetResolutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = Resolver(rootAsset, _ => new ValueTask<AIAssetResolutionResult>(completion.Task));
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();
        var relationship = Required(root, Reference(target, Urn("target")), 0);
        var task = operation.ProcessRelationshipAsync(Path(root, relationship), relationship).AsTask();

        completion.SetException(new InvalidOperationException("resolver failed"));

        Func<Task> act = async () => await task;
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("resolver failed");
        operation.TerminalFailure.Should().BeNull();
    }

    [Fact]
    public async Task CallerCancellationWithoutPriorTerminalCommitment_ShouldRemainCancellation()
    {
        var root = Key(AssetType.Library);
        var rootAsset = Asset(root, Urn("root"));
        var target = Key(AssetType.Knowledge);
        var targetUrn = Urn("target");
        var targetAsset = Asset(target, targetUrn);
        var completion = new TaskCompletionSource<AIAssetResolutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = Resolver(rootAsset, _ => new ValueTask<AIAssetResolutionResult>(completion.Task));
        var operation = new AIAssetGraphResolutionOperation(resolver, root);
        await operation.ResolveRootAsync();
        using var cancellation = new CancellationTokenSource();
        var relationship = Required(root, Reference(target, targetUrn), 0);
        var task = operation.ProcessRelationshipAsync(Path(root, relationship), relationship, cancellation.Token).AsTask();

        cancellation.Cancel();
        completion.SetResult(new AIAssetResolutionResult.Resolved(targetAsset));

        Func<Task> act = async () => await task;
        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.CancellationToken.Should().Be(cancellation.Token);
        operation.TerminalFailure.Should().BeNull();
    }

    private static ScriptedResolver Resolver(
        IAsset rootAsset,
        Func<AssetReference, ValueTask<AIAssetResolutionResult>> reference) =>
        new(rootAsset, reference);

    private static AIAssetGraphRelationship Required(
        AssetDefinitionKey source,
        AssetReference target,
        int ordinal) =>
        new(
            source,
            target,
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphBoundaryRole.External,
            true,
            ordinal,
            $"$.dependencies[{ordinal}]");

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

    private sealed class ScriptedResolver : IPersistentAIAssetResolver
    {
        private readonly IAsset rootAsset;
        private readonly Func<AssetReference, ValueTask<AIAssetResolutionResult>> reference;

        internal ScriptedResolver(
            IAsset rootAsset,
            Func<AssetReference, ValueTask<AIAssetResolutionResult>> reference)
        {
            this.rootAsset = rootAsset;
            this.reference = reference;
        }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.Resolved(rootAsset));

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
