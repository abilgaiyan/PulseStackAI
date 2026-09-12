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
    public void Project_ShouldEnumerateStructuralOwnershipThenPartitionedDependencies()
    {
        var entry = Reference(AssetType.Workflow, "entry");
        var owned = Reference(AssetType.Tool, "owned");
        var required = Reference(AssetType.Prompt, "required");
        var optional = Reference(AssetType.Model, "optional");
        var project = Construct<ProjectAsset>(
            AssetId.New(),
            Urn("project"),
            new ProjectAssetOptions
            {
                Name = "project",
                EntryWorkflow = entry,
                OwnedAssets = new[] { entry, owned }
            },
            new AssetDependency[]
            {
                new(optional, false),
                new(required, true)
            });

        var relationships = Enumerate(project);

        relationships.Select(static r => r.AuthoredPath).Should().Equal(
            "$.entryWorkflow",
            "$.ownedAssets[0]",
            "$.ownedAssets[1]",
            "$.dependencies[1]",
            "$.dependencies[0]");
        relationships.Select(static r => r.RelationshipClass).Should().Equal(
            AIAssetGraphRelationshipClass.DistinguishedStructural,
            AIAssetGraphRelationshipClass.InternalOwnership,
            AIAssetGraphRelationshipClass.InternalOwnership,
            AIAssetGraphRelationshipClass.ExplicitRequirement,
            AIAssetGraphRelationshipClass.ExplicitRequirement);
        relationships.Select(static r => r.BoundaryRole).Should().Equal(
            AIAssetGraphBoundaryRole.Structural,
            AIAssetGraphBoundaryRole.Internal,
            AIAssetGraphBoundaryRole.Internal,
            AIAssetGraphBoundaryRole.External,
            AIAssetGraphBoundaryRole.External);
        relationships.Select(static r => r.LocalOrdinal).Should().Equal(0, 1, 2, 3, 4);
        relationships[0].TargetReference.Should().BeSameAs(entry);
        relationships[1].TargetReference.Should().BeSameAs(entry);
        relationships[3].DependencyRequired.Should().BeTrue();
        relationships[4].DependencyRequired.Should().BeFalse();
        relationships[4].MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Excluded);
    }

    [Fact]
    public void Library_ShouldEnumerateMembersThenDependencies()
    {
        var member0 = Reference(AssetType.Agent, "m0");
        var member1 = Reference(AssetType.Prompt, "m1");
        var dependency = Reference(AssetType.Tool, "d0");
        var library = Construct<LibraryAsset>(
            AssetId.New(),
            Urn("library"),
            new LibraryAssetOptions { Name = "library", Members = new[] { member0, member1 } },
            new AssetDependency[] { new(dependency) });

        var relationships = Enumerate(library);

        relationships.Select(static r => r.AuthoredPath).Should().Equal("$.members[0]", "$.members[1]", "$.dependencies[0]");
        relationships[0].RelationshipClass.Should().Be(AIAssetGraphRelationshipClass.InternalMembership);
        relationships[1].RelationshipClass.Should().Be(AIAssetGraphRelationshipClass.InternalMembership);
        relationships[2].BoundaryRole.Should().Be(AIAssetGraphBoundaryRole.External);
    }

    [Fact]
    public void Package_ShouldEnumerateDistributionMembersThenDependencies()
    {
        var member = Reference(AssetType.Library, "member");
        var dependency = Reference(AssetType.Model, "dependency");
        var package = Construct<PackageAsset>(
            AssetId.New(),
            Urn("package"),
            new PackageAssetOptions { Name = "package", Description = "package", Members = new[] { member } },
            new AssetDependency[] { new(dependency, false) });

        var relationships = Enumerate(package);

        relationships.Select(static r => r.AuthoredPath).Should().Equal("$.members[0]", "$.dependencies[0]");
        relationships[0].RelationshipClass.Should().Be(AIAssetGraphRelationshipClass.InternalDistribution);
        relationships[0].BoundaryRole.Should().Be(AIAssetGraphBoundaryRole.Internal);
        relationships[1].BoundaryRole.Should().Be(AIAssetGraphBoundaryRole.External);
        relationships[1].MaterializationAuthority.Should().Be(AIAssetGraphMaterializationAuthority.Excluded);
    }

    [Fact]
    public void Agent_ShouldEnumerateTypedReferencesInFrozenOrderThenDependencies()
    {
        var model = Reference(AssetType.Model, "model");
        var prompt = Reference(AssetType.Prompt, "prompt");
        var knowledge0 = Reference(AssetType.Knowledge, "knowledge0");
        var knowledge1 = Reference(AssetType.Knowledge, "knowledge1");
        var tool = Reference(AssetType.Tool, "tool");
        var memory = Reference(AssetType.Memory, "memory");
        var policy = Reference(AssetType.Policy, "policy");
        var unrelatedProjection = Reference(AssetType.Tool, "opaque");
        var agent = Construct<AgentDefinition>(
            AssetId.New(),
            Urn("agent"),
            new AgentDefinitionOptions
            {
                Name = "agent",
                Goal = "goal",
                Role = "role",
                Model = model,
                Prompt = prompt,
                Knowledge = new[] { knowledge0, knowledge1 },
                Tools = new[] { tool },
                Memory = memory,
                Policies = new[] { policy }
            }) with
        {
            References = new[] { unrelatedProjection },
            Dependencies = new[]
            {
                new AssetDependency(Reference(AssetType.Prompt, "optional"), false),
                new AssetDependency(Reference(AssetType.Tool, "required"), true)
            }
        };

        var relationships = Enumerate(agent);

        relationships.Select(static r => r.AuthoredPath).Should().Equal(
            "$.model",
            "$.prompt",
            "$.knowledge[0]",
            "$.knowledge[1]",
            "$.tools[0]",
            "$.memory",
            "$.policies[0]",
            "$.dependencies[1]",
            "$.dependencies[0]");
        relationships.Take(7).Should().OnlyContain(static r => r.RelationshipClass == AIAssetGraphRelationshipClass.DeclarativeReference);
        relationships.Take(7).Should().OnlyContain(static r => r.DependencyRequired == null);
        relationships.Should().NotContain(r => ReferenceEquals(r.TargetReference, unrelatedProjection));
        relationships.Select(static r => r.LocalOrdinal).Should().Equal(Enumerable.Range(0, 9));
    }

    [Fact]
    public void Workflow_ShouldWalkNestedStructureInFrozenOrderAndPreserveDuplicateRuns()
    {
        var duplicate = Reference(AssetType.Agent, "duplicate");
        var thenAgent = Reference(AssetType.Agent, "then");
        var elseAgent = Reference(AssetType.Agent, "else");
        var retryAgent = Reference(AssetType.Agent, "retry");
        var loopAgent = Reference(AssetType.Agent, "loop");
        var case0 = Reference(AssetType.Agent, "case0");
        var case1 = Reference(AssetType.Agent, "case1");
        var defaultAgent = Reference(AssetType.Agent, "default");

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
                        Name = "if",
                        Condition = null!,
                        ThenStep = new RunStepDefinition { Agent = thenAgent },
                        ElseStep = new RunStepDefinition { Agent = elseAgent }
                    }
                }
            },
            new RetryStepDefinition { Step = new RunStepDefinition { Agent = retryAgent } },
            new LoopStepDefinition { Items = null!, Step = new RunStepDefinition { Agent = loopAgent } },
            new SwitchStepDefinition
            {
                Selector = null!,
                Cases = new[]
                {
                    new SwitchCaseDefinition { Value = "a", Step = new RunStepDefinition { Agent = case0 } },
                    new SwitchCaseDefinition { Value = "b", Step = new RunStepDefinition { Agent = case1 } }
                },
                DefaultStep = new RunStepDefinition { Agent = defaultAgent }
            }
        };

        var workflow = Construct<WorkflowAsset>(
            AssetId.New(),
            Urn("workflow"),
            new WorkflowAssetOptions { Name = "workflow", Steps = steps }) with
        {
            Dependencies = new[]
            {
                new AssetDependency(Reference(AssetType.Tool, "optional"), false),
                new AssetDependency(Reference(AssetType.Prompt, "required"), true)
            }
        };

        var relationships = Enumerate(workflow);

        relationships.Select(static r => r.AuthoredPath).Should().Equal(
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
        relationships[0].TargetReference.Should().BeSameAs(duplicate);
        relationships[1].TargetReference.Should().BeSameAs(duplicate);
        relationships.Take(9).Should().OnlyContain(static r => r.RelationshipClass == AIAssetGraphRelationshipClass.DeclarativeReference);
        relationships.Take(9).Should().OnlyContain(static r => r.MaterializationAuthority == AIAssetGraphMaterializationAuthority.Required);
        relationships.Select(static r => r.LocalOrdinal).Should().Equal(Enumerable.Range(0, 11));
    }

    [Theory]
    [InlineData(AssetType.Prompt)]
    [InlineData(AssetType.Tool)]
    [InlineData(AssetType.Knowledge)]
    [InlineData(AssetType.Memory)]
    [InlineData(AssetType.Policy)]
    [InlineData(AssetType.Model)]
    public void FoundationAssets_ShouldEnumerateOnlyDependenciesAndIgnoreReferences(AssetType type)
    {
        var projection = Reference(AssetType.Agent, "reference");
        var required = Reference(AssetType.Tool, "required");
        var optional = Reference(AssetType.Prompt, "optional");
        var source = new TestAsset(
            type,
            new[] { projection },
            new[]
            {
                new AssetDependency(optional, false),
                new AssetDependency(required, true)
            });

        var relationships = Enumerate(source);

        relationships.Select(static r => r.AuthoredPath).Should().Equal("$.dependencies[1]", "$.dependencies[0]");
        relationships.Should().NotContain(r => ReferenceEquals(r.TargetReference, projection));
        relationships.Should().OnlyContain(static r => r.RelationshipClass == AIAssetGraphRelationshipClass.ExplicitRequirement);
        relationships.Should().OnlyContain(static r => r.BoundaryRole == AIAssetGraphBoundaryRole.NotApplicable);
    }

    [Fact]
    public void DependencyPartition_ShouldPreserveAuthoredOrderAndOriginalIndices()
    {
        var optionalA = Reference(AssetType.Prompt, "optional-a");
        var requiredB = Reference(AssetType.Tool, "required-b");
        var optionalC = Reference(AssetType.Model, "optional-c");
        var requiredD = Reference(AssetType.Knowledge, "required-d");
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

        var relationships = Enumerate(source);

        relationships.Select(static r => r.TargetReference).Should().Equal(requiredB, requiredD, optionalA, optionalC);
        relationships.Select(static r => r.AuthoredPath).Should().Equal("$.dependencies[1]", "$.dependencies[3]", "$.dependencies[0]", "$.dependencies[2]");
        relationships.Select(static r => r.LocalOrdinal).Should().Equal(0, 1, 2, 3);
        relationships.Select(static r => r.DependencyRequired).Should().Equal(true, true, false, false);
    }

    [Fact]
    public void Provider_ShouldBeRejectedAsInternalContractMisuse()
    {
        var provider = new TestAsset(AssetType.Provider, Array.Empty<AssetReference>(), Array.Empty<AssetDependency>());

        Action act = () => Enumerate(provider);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CancelledCallerToken_ShouldBePreserved()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var source = new TestAsset(AssetType.Prompt, Array.Empty<AssetReference>(), Array.Empty<AssetDependency>());

        Action act = () => Enumerate(source, cts.Token);

        act.Should().Throw<OperationCanceledException>()
            .Where(exception => exception.CancellationToken == cts.Token);
    }

    [Fact]
    public void ReturnedCollection_ShouldBeReadOnlyAndSourceCollectionsRemainUnchanged()
    {
        var references = new List<AssetReference> { Reference(AssetType.Agent, "opaque") };
        var dependencies = new List<AssetDependency> { new(Reference(AssetType.Tool, "dep")) };
        var source = new TestAsset(AssetType.Prompt, references, dependencies);

        var relationships = Enumerate(source);

        references.Should().HaveCount(1);
        dependencies.Should().ContainSingle();
        relationships.Should().ContainSingle();
        relationships.Should().BeAssignableTo<System.Collections.IList>();
        var list = (System.Collections.IList)relationships;
        list.IsReadOnly.Should().BeTrue();
        Action mutate = () => list.Add(relationships[0]);
        mutate.Should().Throw<NotSupportedException>();
    }

    private static IReadOnlyList<AIAssetGraphRelationship> Enumerate(
        IAsset asset,
        CancellationToken cancellationToken = default)
    {
        var assembly = Assembly.Load("PulseStack.Core");
        var type = assembly.GetType(
            "PulseStack.Core.Persistence.AIAssets.GraphLoading.AIAssetGraphRelationshipEnumerator",
            throwOnError: true)!;
        var instance = Activator.CreateInstance(type, nonPublic: true)!;
        var method = type.GetMethod("Enumerate", BindingFlags.Instance | BindingFlags.NonPublic)!;

        try
        {
            return (IReadOnlyList<AIAssetGraphRelationship>)method.Invoke(instance, new object?[] { asset, cancellationToken })!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static T Construct<T>(params object?[] arguments)
        where T : class
    {
        var constructor = typeof(T)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(ctor => ctor.GetParameters().Length == arguments.Length)
            .First(ctor => ParametersMatch(ctor.GetParameters(), arguments));

        return (T)constructor.Invoke(arguments);
    }

    private static bool ParametersMatch(ParameterInfo[] parameters, object?[] arguments)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (arguments[index] is null)
            {
                continue;
            }

            if (!parameters[index].ParameterType.IsInstanceOfType(arguments[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static AssetReference Reference(AssetType type, string suffix) =>
        new(type, AssetId.New(), Urn(suffix), new AssetVersion("1.0"));

    private static AssetUrn Urn(string suffix) => new($"urn:pulsestack:test:{suffix}:{Guid.NewGuid():N}");

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
            Urn = Urn(type.ToString().ToLowerInvariant());
            Version = new AssetVersion("1.0");
            Metadata = new AssetMetadata { Name = "test" };
            Lifecycle = AssetLifecycle.Published;
            References = references;
            Dependencies = dependencies;
        }
    }
}
