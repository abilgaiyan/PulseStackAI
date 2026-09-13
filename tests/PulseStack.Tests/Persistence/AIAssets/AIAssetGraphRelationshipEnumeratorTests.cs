using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.GraphLoading;
using PulseStack.Abstractions.Workflows.Definitions;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetGraphRelationshipEnumeratorTests
{
    [Fact]
    public void AggregateAssets_ShouldUseFrozenCategoryOrderAndBoundaryRoles()
    {
        var entry = Ref(AssetType.Workflow, "entry");
        var owned = Ref(AssetType.Tool, "owned");
        var project = Construct<ProjectAsset>(
            AssetId.New(), MakeUrn("project"),
            new ProjectAssetOptions { Name = "project", EntryWorkflow = entry, OwnedAssets = new[] { entry, owned } },
            new AssetDependency[]
            {
                new(Ref(AssetType.Model, "optional"), false),
                new(Ref(AssetType.Prompt, "required"), true)
            });

        var library = Construct<LibraryAsset>(
            AssetId.New(), MakeUrn("library"),
            new LibraryAssetOptions { Name = "library", Description = "library", Members = new[] { Ref(AssetType.Agent, "member") } },
            new AssetDependency[] { new(Ref(AssetType.Tool, "dependency")) });

        var package = Construct<PackageAsset>(
            AssetId.New(), MakeUrn("package"),
            new PackageAssetOptions { Name = "package", Description = "package", Members = new[] { Ref(AssetType.Library, "member") } },
            new AssetDependency[] { new(Ref(AssetType.Model, "dependency"), false) });

        var projectEdges = Enumerate(project);
        var libraryEdges = Enumerate(library);
        var packageEdges = Enumerate(package);

        projectEdges.Select(static x => x.AuthoredPath).Should().Equal(
            "$.entryWorkflow", "$.ownedAssets[0]", "$.ownedAssets[1]", "$.dependencies[1]", "$.dependencies[0]");
        projectEdges.Select(static x => x.RelationshipClass).Should().Equal(
            AIAssetGraphRelationshipClass.DistinguishedStructural,
            AIAssetGraphRelationshipClass.InternalOwnership,
            AIAssetGraphRelationshipClass.InternalOwnership,
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphRelationshipClass.ExplicitRequirement);
        projectEdges.Select(static x => x.BoundaryRole).Should().Equal(
            AIAssetGraphBoundaryRole.Structural,
            AIAssetGraphBoundaryRole.Internal,
            AIAssetGraphBoundaryRole.Internal,
            AIAssetGraphBoundaryRole.External,
            AIAssetGraphBoundaryRole.External);
        projectEdges[0].TargetReference.Should().BeSameAs(entry);
        projectEdges[1].TargetReference.Should().BeSameAs(entry);
        projectEdges.Select(static x => x.LocalOrdinal).Should().Equal(0, 1, 2, 3, 4);

        libraryEdges[0].RelationshipClass.Should().Be(AIAssetGraphRelationshipClass.InternalMembership);
        libraryEdges[0].BoundaryRole.Should().Be(AIAssetGraphBoundaryRole.Internal);
        libraryEdges[1].BoundaryRole.Should().Be(AIAssetGraphBoundaryRole.External);

        packageEdges[0].RelationshipClass.Should().Be(AIAssetGraphRelationshipClass.InternalDistribution);
        packageEdges[0].BoundaryRole.Should().Be(AIAssetGraphBoundaryRole.Internal);
        packageEdges[1].BoundaryRole.Should().Be(AIAssetGraphBoundaryRole.External);
        packageEdges[1].MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Excluded);
    }

    [Fact]
    public void Agent_ShouldEnumerateTypedReferencesThenPartitionedDependencies_WithoutUsingReferencesProjection()
    {
        var opaque = Ref(AssetType.Tool, "opaque");
        var agent = Construct<AgentDefinition>(
            AssetId.New(), MakeUrn("agent"),
            new AgentDefinitionOptions
            {
                Name = "agent", Goal = "goal", Role = "role",
                Model = Ref(AssetType.Model, "model"),
                Prompt = Ref(AssetType.Prompt, "prompt"),
                Knowledge = new[] { Ref(AssetType.Knowledge, "k0"), Ref(AssetType.Knowledge, "k1") },
                Tools = new[] { Ref(AssetType.Tool, "tool") },
                Memory = Ref(AssetType.Memory, "memory"),
                Policies = new[] { Ref(AssetType.Policy, "policy") }
            }) with
        {
            References = new[] { opaque },
            Dependencies = new[]
            {
                new AssetDependency(Ref(AssetType.Prompt, "optional"), false),
                new AssetDependency(Ref(AssetType.Tool, "required"), true)
            }
        };

        var edges = Enumerate(agent);

        edges.Select(static x => x.AuthoredPath).Should().Equal(
            "$.model", "$.prompt", "$.knowledge[0]", "$.knowledge[1]", "$.tools[0]", "$.memory", "$.policies[0]",
            "$.dependencies[1]", "$.dependencies[0]");
        edges.Take(7).Should().OnlyContain(static x => x.RelationshipClass == AIAssetGraphRelationshipClass.DeclarativeReference);
        edges.Take(7).Should().OnlyContain(static x => x.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Required);
        edges.Take(7).Should().OnlyContain(static x => x.DependencyRequired == null);
        edges.Should().NotContain(x => ReferenceEquals(x.TargetReference, opaque));
        edges.Select(static x => x.LocalOrdinal).Should().Equal(Enumerable.Range(0, 9));
    }

    [Fact]
    public void Workflow_ShouldWalkNestedStepsInFrozenOrder_AndPreserveDuplicateRunOccurrences()
    {
        var duplicate = Ref(AssetType.Agent, "duplicate");
        var steps = new WorkflowStepDefinition[]
        {
            new RunStepDefinition { Agent = duplicate },
            new ParallelStepDefinition
            {
                Name = "parallel",
                Steps = new WorkflowStepDefinition[]
                {
                    new RunStepDefinition { Agent = duplicate },
                    new ConditionalStepDefinition
                    {
                        Name = "if", Condition = null!,
                        ThenStep = new RunStepDefinition { Agent = Ref(AssetType.Agent, "then") },
                        ElseStep = new RunStepDefinition { Agent = Ref(AssetType.Agent, "else") }
                    }
                }
            },
            new RetryStepDefinition { Step = new RunStepDefinition { Agent = Ref(AssetType.Agent, "retry") } },
            new LoopStepDefinition { Items = null!, Step = new RunStepDefinition { Agent = Ref(AssetType.Agent, "loop") } },
            new SwitchStepDefinition
            {
                Selector = null!,
                Cases = new[]
                {
                    new SwitchCaseDefinition { Value = "a", Step = new RunStepDefinition { Agent = Ref(AssetType.Agent, "case0") } },
                    new SwitchCaseDefinition { Value = "b", Step = new RunStepDefinition { Agent = Ref(AssetType.Agent, "case1") } }
                },
                DefaultStep = new RunStepDefinition { Agent = Ref(AssetType.Agent, "default") }
            }
        };

        var workflow = Construct<WorkflowAsset>(
            AssetId.New(), MakeUrn("workflow"), new WorkflowAssetOptions { Name = "workflow", Steps = steps }) with
        {
            Dependencies = new[]
            {
                new AssetDependency(Ref(AssetType.Tool, "optional"), false),
                new AssetDependency(Ref(AssetType.Prompt, "required"), true)
            }
        };

        var edges = Enumerate(workflow);

        edges.Select(static x => x.AuthoredPath).Should().Equal(
            "$.steps[0].agent",
            "$.steps[1].children[0].agent",
            "$.steps[1].children[1].then.agent",
            "$.steps[1].children[1].else.agent",
            "$.steps[2].child.agent",
            "$.steps[3].child.agent",
            "$.steps[4].cases[0].child.agent",
            "$.steps[4].cases[1].child.agent",
            "$.steps[4].default.agent",
            "$.dependencies[1]",
            "$.dependencies[0]");
        edges[0].TargetReference.Should().BeSameAs(duplicate);
        edges[1].TargetReference.Should().BeSameAs(duplicate);
        edges.Take(9).Should().OnlyContain(static x => x.RelationshipClass == AIAssetGraphRelationshipClass.DeclarativeReference);
        edges.Select(static x => x.LocalOrdinal).Should().Equal(Enumerable.Range(0, 11));
    }

    [Theory]
    [InlineData(AssetType.Prompt)]
    [InlineData(AssetType.Tool)]
    [InlineData(AssetType.Knowledge)]
    [InlineData(AssetType.Memory)]
    [InlineData(AssetType.Policy)]
    [InlineData(AssetType.Model)]
    public void FoundationAssets_ShouldEnumerateDependenciesOnly(AssetType type)
    {
        var opaqueReference = Ref(AssetType.Agent, "opaque");
        var source = new TestAsset(
            type,
            new[] { opaqueReference },
            new[]
            {
                new AssetDependency(Ref(AssetType.Prompt, "optional"), false),
                new AssetDependency(Ref(AssetType.Tool, "required"), true)
            });

        var edges = Enumerate(source);

        edges.Select(static x => x.AuthoredPath).Should().Equal("$.dependencies[1]", "$.dependencies[0]");
        edges.Should().NotContain(x => ReferenceEquals(x.TargetReference, opaqueReference));
        edges.Should().OnlyContain(static x => x.RelationshipClass == AIAssetGraphRelationshipClass.ExplicitRequirement);
        edges.Should().OnlyContain(static x => x.BoundaryRole == AIAssetGraphBoundaryRole.NotApplicable);
    }

    [Fact]
    public void Dependencies_ShouldPartitionRequiredBeforeOptional_WhilePreservingOriginalIndices()
    {
        var optionalA = Ref(AssetType.Prompt, "optional-a");
        var requiredB = Ref(AssetType.Tool, "required-b");
        var optionalC = Ref(AssetType.Model, "optional-c");
        var requiredD = Ref(AssetType.Knowledge, "required-d");
        var source = new TestAsset(
            AssetType.Prompt,
            Array.Empty<AssetReference>(),
            new[]
            {
                new AssetDependency(optionalA, false),
                new AssetDependency(requiredB, true),
                new AssetDependency(optionalC, false),
                new AssetDependency(requiredD, true)
            });

        var edges = Enumerate(source);

        edges.Select(static x => x.TargetReference).Should().Equal(requiredB, requiredD, optionalA, optionalC);
        edges.Select(static x => x.AuthoredPath).Should().Equal("$.dependencies[1]", "$.dependencies[3]", "$.dependencies[0]", "$.dependencies[2]");
        edges.Select(static x => x.DependencyRequired).Should().Equal(true, true, false, false);
        edges.Select(static x => x.MaterializationAuthority).Should().Equal(
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphMaterializationAuthority.Required,
            AIAssetGraphMaterializationAuthority.Excluded,
            AIAssetGraphMaterializationAuthority.Excluded);
    }

    [Fact]
    public void Enumerator_ShouldRejectProvider_PreserveCallerCancellation_AndReturnReadOnlySnapshot()
    {
        var provider = new TestAsset(AssetType.Provider, Array.Empty<AssetReference>(), Array.Empty<AssetDependency>());
        Action unsupported = () => Enumerate(provider);
        unsupported.Should().Throw<InvalidOperationException>();

        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var prompt = new TestAsset(AssetType.Prompt, Array.Empty<AssetReference>(), Array.Empty<AssetDependency>());
        Action cancelled = () => Enumerate(prompt, cts.Token);
        cancelled.Should().Throw<OperationCanceledException>()
            .Where(exception => exception.CancellationToken == cts.Token);

        var references = new List<AssetReference> { Ref(AssetType.Agent, "opaque") };
        var dependencies = new List<AssetDependency> { new(Ref(AssetType.Tool, "dep")) };
        var source = new TestAsset(AssetType.Prompt, references, dependencies);
        var edges = Enumerate(source);

        references.Should().HaveCount(1);
        dependencies.Should().ContainSingle();
        var list = (System.Collections.IList)edges;
        list.IsReadOnly.Should().BeTrue();
        Action mutate = () => list.Add(edges[0]);
        mutate.Should().Throw<NotSupportedException>();
    }

    private static IReadOnlyList<AIAssetGraphRelationship> Enumerate(IAsset asset, CancellationToken token = default)
    {
        var type = Assembly.Load("PulseStack.Core").GetType(
            "PulseStack.Core.Persistence.AIAssets.GraphLoading.AIAssetGraphRelationshipEnumerator",
            throwOnError: true)!;
        var instance = Activator.CreateInstance(type, nonPublic: true)!;
        var method = type.GetMethod("Enumerate", BindingFlags.Instance | BindingFlags.NonPublic)!;

        try
        {
            return (IReadOnlyList<AIAssetGraphRelationship>)method.Invoke(instance, new object?[] { asset, token })!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static T Construct<T>(params object?[] arguments) where T : class
    {
        var constructor = typeof(T)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(x => x.GetParameters().Length == arguments.Length)
            .First(x => ParametersMatch(x.GetParameters(), arguments));
        return (T)constructor.Invoke(arguments);
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, object?[] arguments)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (arguments[index] is not null && !parameters[index].ParameterType.IsInstanceOfType(arguments[index]))
            {
                return false;
            }
        }
        return true;
    }

    private static AssetReference Ref(AssetType type, string suffix) =>
        new(type, AssetId.New(), MakeUrn(suffix), new AssetVersion("1.0"));

    private static AssetUrn MakeUrn(string suffix) =>
        new($"urn:pulsestack:test:{suffix}:{Guid.NewGuid():N}");

    private sealed record TestAsset : Asset
    {
        [SetsRequiredMembers]
        public TestAsset(
            AssetType type,
            IReadOnlyCollection<AssetReference> references,
            IReadOnlyCollection<AssetDependency> dependencies)
            : base(type)
        {
            Id = AssetId.New();
            Urn = AIAssetGraphRelationshipEnumeratorTests.MakeUrn(type.ToString().ToLowerInvariant());
            Version = new AssetVersion("1.0");
            Metadata = new AssetMetadata { Name = "test" };
            Lifecycle = AssetLifecycle.Published;
            References = references;
            Dependencies = dependencies;
        }
    }
}
