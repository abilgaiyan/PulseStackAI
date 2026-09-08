using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Mapping;
using PulseStack.Abstractions.Persistence.AIAssets.Validation;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Values;
using PulseStack.Core.Assets;
using PulseStack.Core.DependencyInjection;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using PulseStack.Core.Persistence.AIAssets.Validation;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class WorkflowMappingConformanceTests
{
    [Fact]
    public async Task CompleteWorkflowLifecycle_ShouldConformAcrossCanonicalMappingBoundary()
    {
        var agentId = AssetId.New();
        var agentV2 = AgentReference(agentId, "2.0.0");
        var agentV1 = AgentReference(agentId, "1.0.0");
        var dependency = new AssetDependency(new AssetReference(
            AssetType.Policy,
            AssetId.New(),
            new AssetUrn("urn:pulsestack:policy:mapping-conformance"),
            new AssetVersion("3.0.0")));

        object?[] literalTree =
        [
            null,
            "text",
            true,
            42,
            12.5m,
            new List<object?> { 1, 2L },
            new Dictionary<string, object?>
            {
                ["z"] = 3,
                ["A"] = "first"
            }
        ];

        var root = new ParallelStepDefinition
        {
            Name = "root",
            Steps =
            [
                new RunStepDefinition { Agent = agentV2 },
                new ConditionalStepDefinition
                {
                    Name = "conditional",
                    Condition = new NamedConditionDefinition { Name = "is-ready" },
                    ThenStep = new RetryStepDefinition
                    {
                        Name = "retry",
                        MaxAttempts = 5,
                        Step = new RunStepDefinition { Agent = agentV1 }
                    },
                    ElseStep = Loop(
                        "input-loop",
                        new InputValueDefinition(),
                        new RunStepDefinition { Agent = agentV2 })
                },
                Loop(
                    "output-loop",
                    new CurrentOutputValueDefinition(),
                    new ParallelStepDefinition { Name = "output-body", Steps = [] }),
                Loop(
                    "context-loop",
                    new ContextItemValueDefinition { Key = "items" },
                    new RunStepDefinition { Agent = agentV1 }),
                Loop(
                    "literal-loop",
                    new LiteralValueDefinition { Value = literalTree },
                    new RunStepDefinition { Agent = agentV2 }),
                new SwitchStepDefinition
                {
                    Name = "switch",
                    Selector = new CurrentOutputValueDefinition(),
                    Cases =
                    [
                        new SwitchCaseDefinition
                        {
                            Value = "retry",
                            Step = new RunStepDefinition { Agent = agentV1 }
                        }
                    ],
                    DefaultStep = new RunStepDefinition { Agent = agentV2 }
                }
            ]
        };

        var seed = CreateWorkflow("Conformance Workflow", "Whole mapping boundary", root);
        var source = seed with
        {
            Version = new AssetVersion("4.2.0"),
            Lifecycle = AssetLifecycle.Published,
            Metadata = seed.Metadata with
            {
                Author = "PulseStackAI",
                Category = "Conformance",
                Tags = ["workflow", "mapping"]
            },
            Dependencies = [dependency]
        };

        IAIAssetDocumentMapper mapper = new AIAssetDocumentMapper();
        IAIAssetDocumentValidator validator = new AIAssetDocumentValidator();

        var document1 = mapper.ToDocument(source)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;
        var validation1 = await validator.ValidateAsync(document1);
        var restored = mapper.FromDocument(document1)
            .Should().BeOfType<WorkflowAsset>().Subject;
        var document2 = mapper.ToDocument(restored)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;
        var validation2 = await validator.ValidateAsync(document2);

        validation1.IsValid.Should().BeTrue();
        validation2.IsValid.Should().BeTrue();
        document2.Should().Be(document1);

        restored.Id.Should().Be(source.Id);
        restored.Urn.Should().Be(source.Urn);
        restored.Version.Should().Be(source.Version);
        restored.Lifecycle.Should().Be(source.Lifecycle);
        restored.Metadata.Should().BeEquivalentTo(source.Metadata);
        restored.Dependencies.Should().Equal(source.Dependencies);
        restored.Options.Name.Should().Be(source.Options.Name);
        restored.Options.Description.Should().Be(source.Options.Description);

        Flatten(source.Options.Steps).Select(step => step.Id)
            .Should().Equal(Flatten(restored.Options.Steps).Select(step => step.Id));
        var reconstructedTypes = Flatten(restored.Options.Steps)
            .Select(step => step.GetType())
            .ToArray();
        reconstructedTypes.Should().Contain(typeof(RunStepDefinition));
        reconstructedTypes.Should().Contain(typeof(ParallelStepDefinition));
        reconstructedTypes.Should().Contain(typeof(ConditionalStepDefinition));
        reconstructedTypes.Should().Contain(typeof(RetryStepDefinition));
        reconstructedTypes.Should().Contain(typeof(LoopStepDefinition));
        reconstructedTypes.Should().Contain(typeof(SwitchStepDefinition));

        restored.References.Should().Equal(agentV2, agentV1);
        var restoredReferences = restored.References.ToArray();
        restoredReferences[0].Type.Should().Be(AssetType.Agent);
        restoredReferences[0].Id.Should().Be(agentV2.Id);
        restoredReferences[0].Urn.Should().Be(agentV2.Urn);
        restoredReferences[0].Version.Should().Be(agentV2.Version);
        restoredReferences[1].Version.Should().Be(agentV1.Version);

        var restoredRoot = restored.Options.Steps.Single()
            .Should().BeOfType<ParallelStepDefinition>().Subject;
        var conditional = restoredRoot.Steps[1]
            .Should().BeOfType<ConditionalStepDefinition>().Subject;
        conditional.Condition.Should().BeOfType<NamedConditionDefinition>()
            .Which.Name.Should().Be("is-ready");
        conditional.ElseStep.Should().NotBeNull();
        conditional.ThenStep.Should().BeOfType<RetryStepDefinition>()
            .Which.MaxAttempts.Should().Be(5);

        var switchStep = restoredRoot.Steps[^1]
            .Should().BeOfType<SwitchStepDefinition>().Subject;
        switchStep.Cases.Should().ContainSingle()
            .Which.Value.Should().Be("retry");
        switchStep.DefaultStep.Should().NotBeNull();

        var restoredLiteral = restoredRoot.Steps[4]
            .Should().BeOfType<LoopStepDefinition>().Subject.Items
            .Should().BeOfType<LiteralValueDefinition>().Subject.Value
            .Should().BeAssignableTo<IReadOnlyList<object?>>().Subject;
        restoredLiteral[0].Should().BeNull();
        restoredLiteral[1].Should().BeOfType<string>();
        restoredLiteral[2].Should().BeOfType<bool>();
        restoredLiteral[3].Should().BeOfType<long>().Which.Should().Be(42L);
        restoredLiteral[4].Should().BeOfType<decimal>();
        restoredLiteral[5].Should().BeAssignableTo<IReadOnlyList<object?>>();
        restoredLiteral[6].Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>();
    }

    [Fact]
    public void CanonicalNormalization_ShouldRemainStableAfterFirstMapping()
    {
        var authored = new List<object?>
        {
            7,
            new[] { 3, 2, 1 },
            new List<object?> { (short)4, (uint)5 },
            new Dictionary<string, object?>
            {
                ["z"] = 9,
                ["A"] = 1,
                ["a"] = 2
            }
        };
        var workflow = CreateWorkflow(
            "Normalization",
            "Canonical CLR normalization",
            Loop(
                "literal-loop",
                new LiteralValueDefinition { Value = authored },
                new ParallelStepDefinition { Name = "body", Steps = [] }));
        var mapper = new AIAssetDocumentMapper();

        var document1 = mapper.ToDocument(workflow)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;
        var asset2 = mapper.FromDocument(document1)
            .Should().BeOfType<WorkflowAsset>().Subject;
        var document2 = mapper.ToDocument(asset2)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

        document2.Should().Be(document1);

        var canonical = asset2.Options.Steps.Single()
            .Should().BeOfType<LoopStepDefinition>().Subject.Items
            .Should().BeOfType<LiteralValueDefinition>().Subject.Value
            .Should().BeAssignableTo<IReadOnlyList<object?>>().Subject;
        canonical[0].Should().BeOfType<long>().Which.Should().Be(7L);
        canonical[1].Should().BeAssignableTo<IReadOnlyList<object?>>();
        canonical[2].Should().BeAssignableTo<IReadOnlyList<object?>>();
        var map = canonical[3]
            .Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;
        map.Should().BeOfType<ReadOnlyDictionary<string, object?>>();
        map.Keys.Should().Equal("A", "a", "z");
    }

    [Fact]
    public void MappingSnapshots_ShouldRemainDetachedFromMutableAuthoringCollections()
    {
        var tags = new List<string> { "initial" };
        var dependencyReference = new AssetReference(
            AssetType.Policy,
            AssetId.New(),
            new AssetUrn("urn:pulsestack:policy:snapshot"),
            AssetVersion.Initial);
        var dependencies = new List<AssetDependency> { new(dependencyReference) };
        var map = new Dictionary<string, object?> { ["a"] = 1 };
        var literalItems = new List<object?> { map, 2 };
        var seed = CreateWorkflow(
            "Snapshot",
            "Detached snapshots",
            Loop(
                "literal-loop",
                new LiteralValueDefinition { Value = literalItems },
                new ParallelStepDefinition { Name = "body", Steps = [] }));
        var source = seed with
        {
            Metadata = seed.Metadata with { Tags = tags },
            Dependencies = dependencies
        };
        var mapper = new AIAssetDocumentMapper();

        var document = mapper.ToDocument(source)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

        tags.Add("mutated");
        dependencies.Clear();
        map["a"] = 99;
        map["z"] = 100;
        literalItems.Add(3);

        document.Metadata.Tags.Should().Equal("initial");
        document.Dependencies.Should().ContainSingle();
        var documentLiteral = document.Steps.Single()
            .Should().BeOfType<LoopStepDocument>().Subject.Items
            .Should().BeOfType<LiteralValueDocument>().Subject.Literal
            .Should().BeOfType<ArrayWorkflowLiteralDocument>().Subject;
        documentLiteral.Items.Should().HaveCount(2);
        var documentMap = documentLiteral.Items[0]
            .Should().BeOfType<ObjectWorkflowLiteralDocument>().Subject;
        documentMap.Properties.Should().ContainSingle()
            .Which.Name.Should().Be("a");
        documentMap.Properties[0].Value.Should().BeOfType<IntegerWorkflowLiteralDocument>()
            .Which.Value.Should().Be(1L);

        var restored = mapper.FromDocument(document)
            .Should().BeOfType<WorkflowAsset>().Subject;
        restored.Metadata.Tags.Should().Equal("initial");
        restored.Dependencies.Should().ContainSingle();
        restored.Metadata.Tags.Should().NotBeSameAs(tags);
        restored.Dependencies.Should().NotBeSameAs(dependencies);

        var reconstructed = restored.Options.Steps.Single()
            .Should().BeOfType<LoopStepDefinition>().Subject.Items
            .Should().BeOfType<LiteralValueDefinition>().Subject.Value
            .Should().BeAssignableTo<IReadOnlyList<object?>>().Subject;
        reconstructed.Should().HaveCount(2);
        var reconstructedMap = reconstructed[0]
            .Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;
        reconstructedMap.Should().BeOfType<ReadOnlyDictionary<string, object?>>();
        reconstructedMap.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, object?>("a", 1L));
    }

    [Fact]
    public async Task InvalidDocument_ShouldFailValidationAndReconstructionWithoutBeingSwallowed()
    {
        var step = new ParallelStepDefinition
        {
            Id = new WorkflowStepId(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            Name = "root",
            Steps = []
        };
        var mapper = new AIAssetDocumentMapper();
        var validator = new AIAssetDocumentValidator();
        var valid = mapper.ToDocument(CreateWorkflow("Invalid boundary", "Malformed probe", step))
            .Should().BeOfType<WorkflowAssetDocument>().Subject;
        var original = valid.Steps.Single()
            .Should().BeOfType<ParallelStepDocument>().Subject;
        var malformedStep = new ParallelStepDocument(
            original.StepId.ToUpperInvariant(),
            original.Name,
            original.Steps);
        var malformed = new WorkflowAssetDocument(
            valid.SchemaVersion,
            valid.Identity,
            valid.Metadata,
            valid.Lifecycle,
            [malformedStep],
            valid.References,
            valid.Dependencies);

        var validation = await validator.ValidateAsync(malformed);
        var reconstruct = () => mapper.FromDocument(malformed);

        validation.IsValid.Should().BeFalse();
        validation.Errors.Should().Contain(error => error.Path == "$.steps[0].stepId");
        reconstruct.Should().Throw<InvalidOperationException>()
            .WithMessage("*$.steps[0].stepId*canonical lowercase non-empty GUID in D format*");
    }

    [Fact]
    public async Task AddPulseStack_ShouldResolvePublicMappingContractsWithoutRuntimeRealization()
    {
        var services = new ServiceCollection();
        services.AddPulseStack();
        using var provider = services.BuildServiceProvider();

        var mapper = provider.GetRequiredService<IAIAssetDocumentMapper>();
        var validator = provider.GetRequiredService<IAIAssetDocumentValidator>();
        var workflow = CreateWorkflow(
            "Composition",
            "Public contract lifecycle",
            new RunStepDefinition { Agent = AgentReference(AssetId.New(), "1.0.0") });

        var document1 = mapper.ToDocument(workflow)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;
        var validation = await validator.ValidateAsync(document1);
        var restored = mapper.FromDocument(document1)
            .Should().BeOfType<WorkflowAsset>().Subject;
        var document2 = mapper.ToDocument(restored)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

        validation.IsValid.Should().BeTrue();
        document2.Should().Be(document1);
        mapper.Should().BeOfType<AIAssetDocumentMapper>();
        validator.Should().BeOfType<AIAssetDocumentValidator>();
        typeof(AIAssetDocumentMapper).GetConstructors().Should().ContainSingle()
            .Which.GetParameters().Should().BeEmpty();
        typeof(AIAssetDocumentValidator).GetConstructors().Should().ContainSingle()
            .Which.GetParameters().Should().BeEmpty();
    }

    private static WorkflowAsset CreateWorkflow(
        string name,
        string description,
        params WorkflowStepDefinition[] steps)
        => new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = name,
                Description = description,
                Steps = steps
            });

    private static LoopStepDefinition Loop(
        string name,
        WorkflowValueDefinition items,
        WorkflowStepDefinition body)
        => new()
        {
            Name = name,
            Items = items,
            Step = body
        };

    private static AssetReference AgentReference(AssetId id, string version)
        => new(
            AssetType.Agent,
            id,
            new AssetUrn($"urn:pulsestack:agent:{id}"),
            new AssetVersion(version));

    private static IEnumerable<WorkflowStepDefinition> Flatten(
        IEnumerable<WorkflowStepDefinition> steps)
    {
        foreach (var step in steps)
        {
            yield return step;

            foreach (var child in Children(step))
            {
                foreach (var nested in Flatten([child]))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<WorkflowStepDefinition> Children(WorkflowStepDefinition step)
        => step switch
        {
            RunStepDefinition => [],
            ParallelStepDefinition parallel => parallel.Steps,
            ConditionalStepDefinition conditional => conditional.ElseStep is null
                ? [conditional.ThenStep]
                : [conditional.ThenStep, conditional.ElseStep],
            RetryStepDefinition retry => [retry.Step],
            LoopStepDefinition loop => [loop.Step],
            SwitchStepDefinition @switch => @switch.DefaultStep is null
                ? @switch.Cases.Select(@case => @case.Step)
                : @switch.Cases.Select(@case => @case.Step).Append(@switch.DefaultStep),
            _ => throw new NotSupportedException($"Unsupported Workflow step '{step.GetType().FullName}'.")
        };
}
