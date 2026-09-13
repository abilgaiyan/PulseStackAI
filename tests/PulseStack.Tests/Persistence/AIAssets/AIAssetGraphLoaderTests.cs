using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Core.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphLoaderTests
{
    [Fact]
    public async Task LoadAsync_ShouldPublishSuccessOnlyAfterCommittedCompletion()
    {
        var child = Foundation(Key(AssetType.Tool, 2), "child");
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(child) });
        var loader = new AIAssetGraphLoader(new ScriptedResolver(root, child));

        var result = await loader.LoadAsync(AssetDefinitionKey.From(root));

        var success = result.Should().BeOfType<AIAssetGraphLoadResult.Success>().Subject;
        var definitionKeys = success.Graph.Nodes.Select(static node => node.DefinitionKey).ToArray();
        definitionKeys.Should().Contain(AssetDefinitionKey.From(root));
        definitionKeys.Should().Contain(AssetDefinitionKey.From(child));
    }

    [Fact]
    public async Task LoadAsync_ShouldPublishAag001ForUnavailableRoot()
    {
        var key = Key(AssetType.Package, 1);
        var loader = new AIAssetGraphLoader(new ScriptedResolver());

        var result = await loader.LoadAsync(key);

        result.Should().BeOfType<AIAssetGraphLoadResult.RootDefinitionUnavailable>();
    }

    [Fact]
    public async Task LoadAsync_ShouldPublishAag002ForUnavailableRequiredDefinition()
    {
        var missing = Foundation(Key(AssetType.Tool, 2), "missing");
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(missing) });
        var loader = new AIAssetGraphLoader(new ScriptedResolver(root));

        var result = await loader.LoadAsync(AssetDefinitionKey.From(root));

        result.Should().BeOfType<AIAssetGraphLoadResult.RequiredDefinitionUnavailable>();
    }

    [Fact]
    public async Task LoadAsync_ShouldPublishAag003ForPersistentReferenceMismatch()
    {
        var child = Foundation(Key(AssetType.Tool, 2), "actual");
        var wrong = new AssetReference(child.Type, child.Id, UrnFor(child, "wrong"), child.Version);
        var root = Package(Key(AssetType.Package, 1), new[] { wrong });
        var loader = new AIAssetGraphLoader(new ScriptedResolver(root, child));

        var result = await loader.LoadAsync(AssetDefinitionKey.From(root));

        result.Should().BeOfType<AIAssetGraphLoadResult.ReferenceIdentityConflict>();
    }

    [Fact]
    public async Task LoadAsync_ShouldPublishAag004ForConflictingLineageIdentity()
    {
        var sharedUrn = new AssetUrn("urn:pulsestack:test:b7:shared-lineage");
        var left = Foundation(Key(AssetType.Tool, 2), sharedUrn);
        var right = Foundation(Key(AssetType.Prompt, 3), sharedUrn);
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(left), Reference(right) });
        var loader = new AIAssetGraphLoader(new ScriptedResolver(root, left, right));

        var result = await loader.LoadAsync(AssetDefinitionKey.From(root));

        result.Should().BeOfType<AIAssetGraphLoadResult.LineageIdentityConflict>();
    }

    [Fact]
    public async Task LoadAsync_ShouldPublishAag005AndNeverSuccessForRequiredCycle()
    {
        var a = Foundation(Key(AssetType.Tool, 2), "a");
        var b = Foundation(Key(AssetType.Prompt, 3), "b");
        a = a with { Dependencies = new[] { new AssetDependency(Reference(b), true) } };
        b = b with { Dependencies = new[] { new AssetDependency(Reference(a), true) } };
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(a) });
        var loader = new AIAssetGraphLoader(new ScriptedResolver(root, a, b));

        var result = await loader.LoadAsync(AssetDefinitionKey.From(root));

        result.Should().BeOfType<AIAssetGraphLoadResult.RequiredMaterializationCycle>();
    }

    [Fact]
    public async Task LoadAsync_ShouldPreservePredecessorExceptionIdentity()
    {
        var key = Key(AssetType.Package, 1);
        var expected = new TestPredecessorException();
        var resolver = new ScriptedResolver { ExactOverride = (_, _) => throw expected };
        var loader = new AIAssetGraphLoader(resolver);

        Func<Task> act = async () => await loader.LoadAsync(key);

        var thrown = await act.Should().ThrowAsync<TestPredecessorException>();
        thrown.Which.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task LoadAsync_ShouldObserveCallerCancellationAfterArgumentValidationAndBeforeOperation()
    {
        var resolver = new ScriptedResolver();
        var loader = new AIAssetGraphLoader(resolver);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = async () => await loader.LoadAsync(Key(AssetType.Package, 1), cts.Token);

        var thrown = await act.Should().ThrowAsync<OperationCanceledException>();
        thrown.Which.CancellationToken.Should().Be(cts.Token);
        resolver.ExactCalls.Should().Be(0);
    }

    [Fact]
    public async Task LoadAsync_ShouldPreserveOneResolutionPerKeyAcrossConvergentPaths()
    {
        var shared = Foundation(Key(AssetType.Tool, 4), "shared");
        var left = Foundation(Key(AssetType.Prompt, 2), "left") with
        {
            Dependencies = new[] { new AssetDependency(Reference(shared), true) }
        };
        var right = Foundation(Key(AssetType.Knowledge, 3), "right") with
        {
            Dependencies = new[] { new AssetDependency(Reference(shared), true) }
        };
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(left), Reference(right) });
        var resolver = new ScriptedResolver(root, left, right, shared);
        var loader = new AIAssetGraphLoader(resolver);

        var result = await loader.LoadAsync(AssetDefinitionKey.From(root));

        result.Should().BeOfType<AIAssetGraphLoadResult.Success>();
        resolver.ReferenceCalls[AssetDefinitionKey.From(shared)].Should().Be(1);
    }

    [Fact]
    public async Task LoadAsync_ShouldIsolateConcurrentInvocations()
    {
        var child = Foundation(Key(AssetType.Tool, 2), "child");
        var root = Package(Key(AssetType.Package, 1), new[] { Reference(child) });
        var resolver = new ScriptedResolver(root, child);
        var loader = new AIAssetGraphLoader(resolver);

        var results = await Task.WhenAll(
            loader.LoadAsync(AssetDefinitionKey.From(root)).AsTask(),
            loader.LoadAsync(AssetDefinitionKey.From(root)).AsTask());

        results.Should().OnlyContain(static result => result is AIAssetGraphLoadResult.Success);
        resolver.ExactCalls.Should().Be(2);
        resolver.ReferenceCalls[AssetDefinitionKey.From(child)].Should().Be(2);
    }

    private static AssetDefinitionKey Key(AssetType type, int value) =>
        new(type, new AssetId(Guid.Parse($"00000000-0000-0000-0000-{value:D12}")), AssetVersion.Initial);

    private static TestAsset Foundation(AssetDefinitionKey key, string suffix) =>
        new(key, new AssetUrn($"urn:pulsestack:test:b7:{suffix}:{key.Id.Value:D}"));

    private static TestAsset Foundation(AssetDefinitionKey key, AssetUrn urn) => new(key, urn);

    private static PackageAsset Package(AssetDefinitionKey key, IReadOnlyList<AssetReference> members) =>
        Construct<PackageAsset>(
            key.Id,
            new AssetUrn($"urn:pulsestack:test:b7:package:{key.Id.Value:D}"),
            key.Version,
            new PackageAssetOptions { Name = "package", Description = "package", Members = members },
            Array.Empty<AssetDependency>());

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static AssetUrn UrnFor(IAsset asset, string suffix) =>
        new($"urn:pulsestack:test:b7:{suffix}:{asset.Id.Value:D}");

    private static T Construct<T>(params object?[] arguments) where T : class
    {
        var constructor = typeof(T)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(candidate => candidate.GetParameters().Length == arguments.Length)
            .First(candidate => ParametersMatch(candidate.GetParameters(), arguments));
        return (T)constructor.Invoke(arguments);
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, object?[] arguments)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (arguments[index] is not null
                && !parameters[index].ParameterType.IsInstanceOfType(arguments[index]))
            {
                return false;
            }
        }
        return true;
    }

    private sealed class ScriptedResolver : IPersistentAIAssetResolver
    {
        private readonly IReadOnlyDictionary<AssetDefinitionKey, IAsset> assets;
        internal Func<AssetDefinitionKey, CancellationToken, ValueTask<AIAssetResolutionResult>>? ExactOverride { get; init; }
        internal int ExactCalls;
        internal Dictionary<AssetDefinitionKey, int> ReferenceCalls { get; } = [];

        internal ScriptedResolver(params IAsset[] assets) =>
            this.assets = assets.ToDictionary(AssetDefinitionKey.From);

        public ValueTask<AIAssetResolutionResult> ResolveAsync(AssetDefinitionKey key, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref ExactCalls);
            if (ExactOverride is not null) return ExactOverride(key, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<AIAssetResolutionResult>(assets.TryGetValue(key, out var asset)
                ? new AIAssetResolutionResult.Resolved(asset)
                : new AIAssetResolutionResult.DefinitionNotPublished());
        }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(AssetReference reference, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = AssetDefinitionKey.From(reference);
            lock (ReferenceCalls)
            {
                ReferenceCalls[key] = ReferenceCalls.TryGetValue(key, out var count) ? count + 1 : 1;
            }
            if (!assets.TryGetValue(key, out var asset))
                return ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.DefinitionNotPublished());
            return ValueTask.FromResult<AIAssetResolutionResult>(asset.Urn == reference.Urn
                ? new AIAssetResolutionResult.Resolved(asset)
                : new AIAssetResolutionResult.ReferenceMismatch());
        }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(AssetUrn urn, AssetVersion version, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<CatalogLineageLookupResult> DiscoverLineageAsync(AssetUrn urn, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TestPredecessorException : Exception;

    private sealed record TestAsset : Asset
    {
        [SetsRequiredMembers]
        internal TestAsset(AssetDefinitionKey key, AssetUrn urn) : base(key.Type)
        {
            Id = key.Id;
            Urn = urn;
            Version = key.Version;
            Metadata = new AssetMetadata { Name = "test" };
            Lifecycle = AssetLifecycle.Published;
        }
    }
}
