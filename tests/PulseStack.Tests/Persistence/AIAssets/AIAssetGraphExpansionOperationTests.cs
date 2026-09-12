using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Core.Persistence.AIAssets.GraphLoading;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphExpansionOperationTests
{
    [Fact]
    public async Task NestedAggregates_ShouldExpandRecursively_PreserveLocalBoundaries_AndConvergeSharedNode()
    {
        var sharedToolKey = Key(AssetType.Tool);
        var sharedTool = Foundation(sharedToolKey, "shared-tool");
        var requiredPromptKey = Key(AssetType.Prompt);
        var requiredPrompt = Foundation(requiredPromptKey, "required-prompt");
        var optionalModelKey = Key(AssetType.Model);
        var optionalModel = Foundation(optionalModelKey, "optional-model");
        var opaqueAgentKey = Key(AssetType.Agent);

        sharedTool = sharedTool with
        {
            References = new[] { Reference(opaqueAgentKey, UrnFor(opaqueAgentKey, "opaque-agent")) }
        };

        var workflowKey = Key(AssetType.Workflow);
        var workflow = Construct<WorkflowAsset>(
            workflowKey.Id,
            UrnFor(workflowKey, "entry-workflow"),
            new WorkflowAssetOptions { Name = "entry", Steps = Array.Empty<WorkflowStepDefinition>() });

        var nestedPackageKey = Key(AssetType.Package);
        var nestedPackage = Construct<PackageAsset>(
            nestedPackageKey.Id,
            UrnFor(nestedPackageKey, "nested-package"),
            new PackageAssetOptions
            {
                Name = "nested-package",
                Description = "nested-package",
                Members = new[] { Reference(sharedTool) }
            },
            new[]
            {
                new AssetDependency(Reference(requiredPrompt), true),
                new AssetDependency(Reference(optionalModel), false)
            });

        var projectKey = Key(AssetType.Project);
        var project = Construct<ProjectAsset>(
            projectKey.Id,
            UrnFor(projectKey, "project"),
            new ProjectAssetOptions
            {
                Name = "project",
                EntryWorkflow = Reference(workflow),
                OwnedAssets = new[] { Reference(sharedTool) }
            },
            Array.Empty<AssetDependency>());

        var libraryKey = Key(AssetType.Library);
        var library = Construct<LibraryAsset>(
            libraryKey.Id,
            UrnFor(libraryKey, "library"),
            new LibraryAssetOptions
            {
                Name = "library",
                Description = "library",
                Members = new[] { Reference(sharedTool) }
            },
            Array.Empty<AssetDependency>());

        var rootKey = Key(AssetType.Package);
        var root = Construct<PackageAsset>(
            rootKey.Id,
            UrnFor(rootKey, "root-package"),
            new PackageAssetOptions
            {
                Name = "root",
                Description = "root",
                Members = new[]
                {
                    Reference(nestedPackage),
                    Reference(project),
                    Reference(library)
                }
            },
            Array.Empty<AssetDependency>());

        var resolver = Resolver(root, nestedPackage, project, library, workflow, sharedTool, requiredPrompt, optionalModel);
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        var failure = await operation.ExpandAsync();

        failure.Should().BeNull();
        operation.MaterializedNodes.Select(static node => node.DefinitionKey).Should().BeEquivalentTo(
            new[] { rootKey, nestedPackageKey, projectKey, libraryKey, workflowKey, sharedToolKey, requiredPromptKey });
        operation.MaterializedNodes.Should().NotContain(node => node.DefinitionKey == optionalModelKey);
        resolver.ReferenceCalls.Should().NotContainKey(optionalModelKey);
        resolver.ReferenceCalls.Should().NotContainKey(opaqueAgentKey);
        resolver.ReferenceCalls[sharedToolKey].Should().Be(1);

        var sharedEdges = operation.ObservedRelationships
            .Where(edge => AssetDefinitionKey.From(edge.TargetReference) == sharedToolKey)
            .ToArray();
        sharedEdges.Should().HaveCount(3);
        sharedEdges.Should().ContainSingle(edge =>
            edge.SourceKey == nestedPackageKey
            && edge.RelationshipClass == AIAssetGraphRelationshipClass.InternalDistribution
            && edge.BoundaryRole == AIAssetGraphBoundaryRole.Internal);
        sharedEdges.Should().ContainSingle(edge =>
            edge.SourceKey == projectKey
            && edge.RelationshipClass == AIAssetGraphRelationshipClass.InternalOwnership
            && edge.BoundaryRole == AIAssetGraphBoundaryRole.Internal);
        sharedEdges.Should().ContainSingle(edge =>
            edge.SourceKey == libraryKey
            && edge.RelationshipClass == AIAssetGraphRelationshipClass.InternalMembership
            && edge.BoundaryRole == AIAssetGraphBoundaryRole.Internal);

        operation.ObservedRelationships.Should().Contain(edge =>
            edge.SourceKey == projectKey
            && edge.RelationshipClass == AIAssetGraphRelationshipClass.DistinguishedStructural
            && edge.BoundaryRole == AIAssetGraphBoundaryRole.Structural
            && AssetDefinitionKey.From(edge.TargetReference) == workflowKey);
        operation.ObservedRelationships.Should().Contain(edge =>
            edge.SourceKey == nestedPackageKey
            && edge.RelationshipClass == AIAssetGraphRelationshipClass.ExplicitRequirement
            && edge.BoundaryRole == AIAssetGraphBoundaryRole.External
            && edge.DependencyRequired == true
            && AssetDefinitionKey.From(edge.TargetReference) == requiredPromptKey);
        operation.ObservedRelationships.Should().Contain(edge =>
            edge.SourceKey == nestedPackageKey
            && edge.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Excluded
            && edge.DependencyRequired == false
            && AssetDefinitionKey.From(edge.TargetReference) == optionalModelKey);
    }

    [Fact]
    public async Task WorkflowAndAgent_ShouldExpandRequiredDeclarativeRelationshipsAndDependencies()
    {
        var modelKey = Key(AssetType.Model);
        var model = Foundation(modelKey, "model");
        var toolKey = Key(AssetType.Tool);
        var tool = Foundation(toolKey, "tool");
        var dependencyKey = Key(AssetType.Policy);
        var dependency = Foundation(dependencyKey, "agent-dependency");

        var agentKey = Key(AssetType.Agent);
        var agent = Construct<AgentDefinition>(
            agentKey.Id,
            UrnFor(agentKey, "agent"),
            new AgentDefinitionOptions
            {
                Name = "agent",
                Goal = "goal",
                Role = "role",
                Model = Reference(model),
                Tools = new[] { Reference(tool) }
            }) with
        {
            Dependencies = new[] { new AssetDependency(Reference(dependency), true) }
        };

        var workflowKey = Key(AssetType.Workflow);
        var workflow = Construct<WorkflowAsset>(
            workflowKey.Id,
            UrnFor(workflowKey, "workflow"),
            new WorkflowAssetOptions
            {
                Name = "workflow",
                Steps = new WorkflowStepDefinition[]
                {
                    new RunStepDefinition { Agent = Reference(agent) }
                }
            });

        var rootKey = Key(AssetType.Package);
        var root = Construct<PackageAsset>(
            rootKey.Id,
            UrnFor(rootKey, "root"),
            new PackageAssetOptions
            {
                Name = "root",
                Description = "root",
                Members = new[] { Reference(workflow) }
            },
            Array.Empty<AssetDependency>());

        var resolver = Resolver(root, workflow, agent, model, tool, dependency);
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        var failure = await operation.ExpandAsync();

        failure.Should().BeNull();
        operation.MaterializedNodes.Select(static node => node.DefinitionKey).Should().BeEquivalentTo(
            new[] { rootKey, workflowKey, agentKey, modelKey, toolKey, dependencyKey });
        operation.ObservedRelationships.Should().Contain(edge =>
            edge.SourceKey == workflowKey
            && edge.RelationshipClass == AIAssetGraphRelationshipClass.DeclarativeReference
            && AssetDefinitionKey.From(edge.TargetReference) == agentKey);
        operation.ObservedRelationships.Should().Contain(edge =>
            edge.SourceKey == agentKey
            && edge.RelationshipClass == AIAssetGraphRelationshipClass.DeclarativeReference
            && AssetDefinitionKey.From(edge.TargetReference) == modelKey);
        operation.ObservedRelationships.Should().Contain(edge =>
            edge.SourceKey == agentKey
            && edge.RelationshipClass == AIAssetGraphRelationshipClass.DeclarativeReference
            && AssetDefinitionKey.From(edge.TargetReference) == toolKey);
        operation.ObservedRelationships.Should().Contain(edge =>
            edge.SourceKey == agentKey
            && edge.RelationshipClass == AIAssetGraphRelationshipClass.ExplicitRequirement
            && edge.DependencyRequired == true
            && AssetDefinitionKey.From(edge.TargetReference) == dependencyKey);
    }

    [Fact]
    public async Task SharedDescendant_ShouldConvergeWithoutCycleOrSecondResolution()
    {
        var sharedKey = Key(AssetType.Prompt);
        var shared = Foundation(sharedKey, "shared");
        var leftKey = Key(AssetType.Tool);
        var left = Foundation(leftKey, "left") with
        {
            Dependencies = new[] { new AssetDependency(Reference(shared), true) }
        };
        var rightKey = Key(AssetType.Knowledge);
        var right = Foundation(rightKey, "right") with
        {
            Dependencies = new[] { new AssetDependency(Reference(shared), true) }
        };
        var rootKey = Key(AssetType.Package);
        var root = Construct<PackageAsset>(
            rootKey.Id,
            UrnFor(rootKey, "root"),
            new PackageAssetOptions
            {
                Name = "root",
                Description = "root",
                Members = new[] { Reference(left), Reference(right) }
            },
            Array.Empty<AssetDependency>());

        var resolver = Resolver(root, left, right, shared);
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        var failure = await operation.ExpandAsync();

        failure.Should().BeNull();
        resolver.ReferenceCalls[sharedKey].Should().Be(1);
        operation.MaterializedNodes.Count(node => node.DefinitionKey == sharedKey).Should().Be(1);
        operation.ObservedRelationships.Count(edge => AssetDefinitionKey.From(edge.TargetReference) == sharedKey).Should().Be(2);
    }

    [Fact]
    public async Task RequiredSelfCycle_ShouldReportAag005WithIndexZeroAndNoSecondResolution()
    {
        var rootKey = Key(AssetType.Package);
        var rootUrn = UrnFor(rootKey, "self-root");
        var rootReference = Reference(rootKey, rootUrn);
        var root = Construct<PackageAsset>(
            rootKey.Id,
            rootUrn,
            new PackageAssetOptions
            {
                Name = "root",
                Description = "root",
                Members = new[] { rootReference }
            },
            Array.Empty<AssetDependency>());
        var resolver = Resolver(root);
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        var failure = await operation.ExpandAsync();

        var cycle = failure.Should().BeOfType<AIAssetGraphLoadResult.RequiredMaterializationCycle>().Subject;
        cycle.Context.Code.Should().Be(AIAssetGraphDiagnosticCodes.RequiredMaterializationCycle);
        cycle.Context.CycleEntryKey.Should().Be(rootKey);
        cycle.Context.CycleStartSegmentIndex.Should().Be(0);
        cycle.Context.CanonicalPath.Segments.Should().ContainSingle();
        cycle.Context.CanonicalPath.Segments[^1].TargetReference.Should().BeSameAs(rootReference);
        resolver.ExactCalls[rootKey].Should().Be(1);
        resolver.ReferenceCalls.Should().NotContainKey(rootKey);
    }

    [Fact]
    public async Task RootReentryThroughRequiredDependency_ShouldReportCycleWithoutResolvingRootAgain()
    {
        var rootKey = Key(AssetType.Package);
        var childKey = Key(AssetType.Tool);
        var rootUrn = UrnFor(rootKey, "root");
        var child = Foundation(childKey, "child") with
        {
            Dependencies = new[] { new AssetDependency(Reference(rootKey, rootUrn), true) }
        };
        var root = Construct<PackageAsset>(
            rootKey.Id,
            rootUrn,
            new PackageAssetOptions
            {
                Name = "root",
                Description = "root",
                Members = new[] { Reference(child) }
            },
            Array.Empty<AssetDependency>());
        var resolver = Resolver(root, child);
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        var failure = await operation.ExpandAsync();

        var cycle = failure.Should().BeOfType<AIAssetGraphLoadResult.RequiredMaterializationCycle>().Subject;
        cycle.Context.CycleEntryKey.Should().Be(rootKey);
        cycle.Context.CycleStartSegmentIndex.Should().Be(0);
        cycle.Context.CanonicalPath.Segments.Should().HaveCount(2);
        resolver.ExactCalls[rootKey].Should().Be(1);
        resolver.ReferenceCalls.Should().NotContainKey(rootKey);
    }

    [Fact]
    public async Task PureAggregateCycle_ShouldUseActiveAggregateEntryIndex()
    {
        var rootKey = Key(AssetType.Package);
        var aKey = Key(AssetType.Package);
        var bKey = Key(AssetType.Library);
        var aUrn = UrnFor(aKey, "a");
        var bUrn = UrnFor(bKey, "b");

        var a = Construct<PackageAsset>(
            aKey.Id,
            aUrn,
            new PackageAssetOptions
            {
                Name = "a",
                Description = "a",
                Members = new[] { Reference(bKey, bUrn) }
            },
            Array.Empty<AssetDependency>());
        var b = Construct<LibraryAsset>(
            bKey.Id,
            bUrn,
            new LibraryAssetOptions
            {
                Name = "b",
                Description = "b",
                Members = new[] { Reference(aKey, aUrn) }
            },
            Array.Empty<AssetDependency>());
        var root = Construct<PackageAsset>(
            rootKey.Id,
            UrnFor(rootKey, "root"),
            new PackageAssetOptions
            {
                Name = "root",
                Description = "root",
                Members = new[] { Reference(a) }
            },
            Array.Empty<AssetDependency>());

        var resolver = Resolver(root, a, b);
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        var failure = await operation.ExpandAsync();

        var cycle = failure.Should().BeOfType<AIAssetGraphLoadResult.RequiredMaterializationCycle>().Subject;
        cycle.Context.CycleEntryKey.Should().Be(aKey);
        cycle.Context.CycleStartSegmentIndex.Should().Be(1);
        cycle.Context.CanonicalPath.Segments.Should().HaveCount(3);
        resolver.ReferenceCalls[aKey].Should().Be(1);
    }

    [Fact]
    public async Task PureRequiredDependencyCycle_ShouldReportCycle()
    {
        var rootKey = Key(AssetType.Package);
        var aKey = Key(AssetType.Tool);
        var bKey = Key(AssetType.Prompt);
        var aUrn = UrnFor(aKey, "a");
        var bUrn = UrnFor(bKey, "b");
        var a = Foundation(aKey, aUrn) with
        {
            Dependencies = new[] { new AssetDependency(Reference(bKey, bUrn), true) }
        };
        var b = Foundation(bKey, bUrn) with
        {
            Dependencies = new[] { new AssetDependency(Reference(aKey, aUrn), true) }
        };
        var root = Construct<PackageAsset>(
            rootKey.Id,
            UrnFor(rootKey, "root"),
            new PackageAssetOptions
            {
                Name = "root",
                Description = "root",
                Members = new[] { Reference(a) }
            },
            Array.Empty<AssetDependency>());
        var resolver = Resolver(root, a, b);
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        var failure = await operation.ExpandAsync();

        var cycle = failure.Should().BeOfType<AIAssetGraphLoadResult.RequiredMaterializationCycle>().Subject;
        cycle.Context.CycleEntryKey.Should().Be(aKey);
        cycle.Context.CanonicalPath.Segments[^1].RelationshipClass.Should().Be(AIAssetGraphRelationshipClass.ExplicitRequirement);
        cycle.Context.CanonicalPath.Segments[^1].DependencyRequired.Should().BeTrue();
        resolver.ReferenceCalls[aKey].Should().Be(1);
    }

    [Fact]
    public async Task DeclarativeReferenceParticipatingInCycle_ShouldReportCycle()
    {
        var rootKey = Key(AssetType.Package);
        var workflowKey = Key(AssetType.Workflow);
        var agentKey = Key(AssetType.Agent);
        var workflowUrn = UrnFor(workflowKey, "workflow");
        var agentUrn = UrnFor(agentKey, "agent");

        var agent = Construct<AgentDefinition>(
            agentKey.Id,
            agentUrn,
            new AgentDefinitionOptions { Name = "agent", Goal = "goal", Role = "role" }) with
        {
            Dependencies = new[] { new AssetDependency(Reference(workflowKey, workflowUrn), true) }
        };
        var workflow = Construct<WorkflowAsset>(
            workflowKey.Id,
            workflowUrn,
            new WorkflowAssetOptions
            {
                Name = "workflow",
                Steps = new WorkflowStepDefinition[]
                {
                    new RunStepDefinition { Agent = Reference(agent) }
                }
            });
        var root = Construct<PackageAsset>(
            rootKey.Id,
            UrnFor(rootKey, "root"),
            new PackageAssetOptions
            {
                Name = "root",
                Description = "root",
                Members = new[] { Reference(workflow) }
            },
            Array.Empty<AssetDependency>());
        var resolver = Resolver(root, workflow, agent);
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        var failure = await operation.ExpandAsync();

        var cycle = failure.Should().BeOfType<AIAssetGraphLoadResult.RequiredMaterializationCycle>().Subject;
        cycle.Context.CycleEntryKey.Should().Be(workflowKey);
        cycle.Context.CanonicalPath.Segments.Should().Contain(segment =>
            segment.RelationshipClass == AIAssetGraphRelationshipClass.DeclarativeReference);
        cycle.Context.CanonicalPath.Segments[^1].RelationshipClass.Should().Be(AIAssetGraphRelationshipClass.ExplicitRequirement);
    }

    [Fact]
    public async Task MixedRequiredEdgeCycle_ShouldPreserveEveryRelationshipRole()
    {
        var rootKey = Key(AssetType.Package);
        var projectKey = Key(AssetType.Project);
        var workflowKey = Key(AssetType.Workflow);
        var projectUrn = UrnFor(projectKey, "project");
        var workflowUrn = UrnFor(workflowKey, "workflow");
        var workflow = Construct<WorkflowAsset>(
            workflowKey.Id,
            workflowUrn,
            new WorkflowAssetOptions { Name = "workflow", Steps = Array.Empty<WorkflowStepDefinition>() }) with
        {
            Dependencies = new[] { new AssetDependency(Reference(projectKey, projectUrn), true) }
        };
        var project = Construct<ProjectAsset>(
            projectKey.Id,
            projectUrn,
            new ProjectAssetOptions
            {
                Name = "project",
                EntryWorkflow = Reference(workflowKey, workflowUrn),
                OwnedAssets = Array.Empty<AssetReference>()
            },
            Array.Empty<AssetDependency>());
        var root = Construct<PackageAsset>(
            rootKey.Id,
            UrnFor(rootKey, "root"),
            new PackageAssetOptions
            {
                Name = "root",
                Description = "root",
                Members = new[] { Reference(project) }
            },
            Array.Empty<AssetDependency>());
        var resolver = Resolver(root, project, workflow);
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        var failure = await operation.ExpandAsync();

        var cycle = failure.Should().BeOfType<AIAssetGraphLoadResult.RequiredMaterializationCycle>().Subject;
        cycle.Context.CycleEntryKey.Should().Be(projectKey);
        cycle.Context.CanonicalPath.Segments.Select(static segment => segment.RelationshipClass).Should().Equal(
            AIAssetGraphRelationshipClass.InternalDistribution,
            AIAssetGraphRelationshipClass.DistinguishedStructural,
            AIAssetGraphRelationshipClass.ExplicitRequirement);
    }

    [Fact]
    public async Task OptionalBackReference_ShouldRemainObservableWithoutFormingCycleOrResolution()
    {
        var rootKey = Key(AssetType.Package);
        var childKey = Key(AssetType.Tool);
        var rootUrn = UrnFor(rootKey, "root");
        var child = Foundation(childKey, "child") with
        {
            Dependencies = new[] { new AssetDependency(Reference(rootKey, rootUrn), false) }
        };
        var root = Construct<PackageAsset>(
            rootKey.Id,
            rootUrn,
            new PackageAssetOptions
            {
                Name = "root",
                Description = "root",
                Members = new[] { Reference(child) }
            },
            Array.Empty<AssetDependency>());
        var resolver = Resolver(root, child);
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        var failure = await operation.ExpandAsync();

        failure.Should().BeNull();
        operation.ObservedRelationships.Should().Contain(edge =>
            edge.SourceKey == childKey
            && AssetDefinitionKey.From(edge.TargetReference) == rootKey
            && edge.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Excluded);
        resolver.ExactCalls[rootKey].Should().Be(1);
        resolver.ReferenceCalls.Should().NotContainKey(rootKey);
    }

    [Fact]
    public async Task CancellationDuringRecursiveResolution_ShouldPropagateCallerToken()
    {
        var rootKey = Key(AssetType.Package);
        var childKey = Key(AssetType.Tool);
        var child = Foundation(childKey, "child");
        var root = Construct<PackageAsset>(
            rootKey.Id,
            UrnFor(rootKey, "root"),
            new PackageAssetOptions
            {
                Name = "root",
                Description = "root",
                Members = new[] { Reference(child) }
            },
            Array.Empty<AssetDependency>());
        using var cts = new CancellationTokenSource();
        var observedToken = CancellationToken.None;
        var wait = new TaskCompletionSource<AIAssetResolutionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resolver = new ScriptedResolver(new IAsset[] { root, child })
        {
            ReferenceOverride = (reference, token) =>
            {
                observedToken = token;
                return new ValueTask<AIAssetResolutionResult>(wait.Task);
            }
        };
        var operation = new AIAssetGraphExpansionOperation(resolver, rootKey);

        var task = operation.ExpandAsync(cts.Token).AsTask();
        observedToken.Should().Be(cts.Token);
        cts.Cancel();
        wait.SetCanceled(cts.Token);

        Func<Task> act = async () => await task;
        var exception = await act.Should().ThrowAsync<OperationCanceledException>();
        exception.Which.CancellationToken.Should().Be(cts.Token);
    }

    private static ScriptedResolver Resolver(params IAsset[] assets) => new(assets);

    private static AssetDefinitionKey Key(AssetType type) =>
        new(type, AssetId.New(), new AssetVersion("1.0"));

    private static TestAsset Foundation(AssetDefinitionKey key, string suffix) =>
        new(key, UrnFor(key, suffix));

    private static TestAsset Foundation(AssetDefinitionKey key, AssetUrn urn) =>
        new(key, urn);

    private static AssetReference Reference(IAsset asset) =>
        new(asset.Type, asset.Id, asset.Urn, asset.Version);

    private static AssetReference Reference(AssetDefinitionKey key, AssetUrn urn) =>
        new(key.Type, key.Id, urn, key.Version);

    private static AssetUrn UrnFor(AssetDefinitionKey key, string suffix) =>
        new($"urn:pulsestack:test:{suffix}:{key.Id.Value:D}");

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

    private sealed class ScriptedResolver : IPersistentAIAssetResolver
    {
        private readonly IReadOnlyDictionary<AssetDefinitionKey, IAsset> assets;

        internal ScriptedResolver(IEnumerable<IAsset> assets)
        {
            this.assets = assets.ToDictionary(AssetDefinitionKey.From);
        }

        internal Dictionary<AssetDefinitionKey, int> ExactCalls { get; } = [];
        internal Dictionary<AssetDefinitionKey, int> ReferenceCalls { get; } = [];
        internal Func<AssetReference, CancellationToken, ValueTask<AIAssetResolutionResult>>? ReferenceOverride { get; init; }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetDefinitionKey key,
            CancellationToken cancellationToken = default)
        {
            Increment(ExactCalls, key);
            return ValueTask.FromResult(
                assets.TryGetValue(key, out var asset)
                    ? (AIAssetResolutionResult)new AIAssetResolutionResult.Resolved(asset)
                    : new AIAssetResolutionResult.DefinitionNotPublished());
        }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetReference reference,
            CancellationToken cancellationToken = default)
        {
            var key = AssetDefinitionKey.From(reference);
            Increment(ReferenceCalls, key);

            if (ReferenceOverride is not null)
            {
                return ReferenceOverride(reference, cancellationToken);
            }

            if (!assets.TryGetValue(key, out var asset))
            {
                return ValueTask.FromResult<AIAssetResolutionResult>(new AIAssetResolutionResult.DefinitionNotPublished());
            }

            return ValueTask.FromResult<AIAssetResolutionResult>(
                asset.Urn == reference.Urn
                    ? new AIAssetResolutionResult.Resolved(asset)
                    : new AIAssetResolutionResult.ReferenceMismatch());
        }

        public ValueTask<AIAssetResolutionResult> ResolveAsync(
            AssetUrn urn,
            AssetVersion version,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("URN/version resolution is outside B.4 authority.");

        public ValueTask<CatalogLineageLookupResult> DiscoverLineageAsync(
            AssetUrn urn,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Lineage discovery is outside B.4 authority.");

        private static void Increment(
            Dictionary<AssetDefinitionKey, int> calls,
            AssetDefinitionKey key)
        {
            calls.TryGetValue(key, out var count);
            calls[key] = count + 1;
        }
    }
}
