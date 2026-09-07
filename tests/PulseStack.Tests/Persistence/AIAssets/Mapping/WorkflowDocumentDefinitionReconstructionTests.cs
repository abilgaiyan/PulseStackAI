using System.Collections.ObjectModel;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Values;
using PulseStack.Core.Assets;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class WorkflowDocumentDefinitionReconstructionTests
{
    [Fact]
    public void FromDocument_ShouldReconstructCompleteRecursiveGrammarAndPreserveStepIds()
    {
        var agent = AgentReference();
        var run = new RunStepDefinition { Agent = agent };
        var retry = new RetryStepDefinition
        {
            Name = "retry",
            MaxAttempts = 4,
            Step = new RunStepDefinition { Agent = agent }
        };
        var loop = new LoopStepDefinition
        {
            Name = "loop",
            Items = new ContextItemValueDefinition { Key = "items" },
            Step = retry
        };
        var conditional = new ConditionalStepDefinition
        {
            Name = "conditional",
            Condition = new NamedConditionDefinition { Name = "should-run" },
            ThenStep = loop,
            ElseStep = new RunStepDefinition { Agent = agent }
        };
        var @switch = new SwitchStepDefinition
        {
            Name = "switch",
            Selector = new CurrentOutputValueDefinition(),
            Cases =
            [
                new SwitchCaseDefinition
                {
                    Value = "yes",
                    Step = conditional
                }
            ],
            DefaultStep = new RunStepDefinition { Agent = agent }
        };
        var parallel = new ParallelStepDefinition
        {
            Name = "parallel",
            Steps = [run, @switch]
        };

        var source = CreateWorkflow(parallel);
        var document = Map(source);
        var restored = Restore(document);

        restored.Options.Steps.Should().ContainSingle();
        var restoredParallel = restored.Options.Steps.Single()
            .Should().BeOfType<ParallelStepDefinition>().Subject;
        restoredParallel.Id.Should().Be(parallel.Id);
        restoredParallel.Steps.Should().HaveCount(2);
        restoredParallel.Steps[0].Should().BeOfType<RunStepDefinition>()
            .Which.Id.Should().Be(run.Id);

        var restoredSwitch = restoredParallel.Steps[1]
            .Should().BeOfType<SwitchStepDefinition>().Subject;
        restoredSwitch.Id.Should().Be(@switch.Id);
        restoredSwitch.Selector.Should().BeOfType<CurrentOutputValueDefinition>();
        restoredSwitch.Cases.Should().ContainSingle();

        var restoredConditional = restoredSwitch.Cases[0].Step
            .Should().BeOfType<ConditionalStepDefinition>().Subject;
        restoredConditional.Id.Should().Be(conditional.Id);
        restoredConditional.Condition.Should().BeOfType<NamedConditionDefinition>()
            .Which.Name.Should().Be("should-run");

        var restoredLoop = restoredConditional.ThenStep
            .Should().BeOfType<LoopStepDefinition>().Subject;
        restoredLoop.Id.Should().Be(loop.Id);
        restoredLoop.Items.Should().BeOfType<ContextItemValueDefinition>()
            .Which.Key.Should().Be("items");

        var restoredRetry = restoredLoop.Step
            .Should().BeOfType<RetryStepDefinition>().Subject;
        restoredRetry.Id.Should().Be(retry.Id);
        restoredRetry.MaxAttempts.Should().Be(4);
        restoredRetry.Step.Should().BeOfType<RunStepDefinition>();
        restoredConditional.ElseStep.Should().BeOfType<RunStepDefinition>();
        restoredSwitch.DefaultStep.Should().BeOfType<RunStepDefinition>();
    }

    [Fact]
    public void FromDocument_ShouldReconstructAllWorkflowValueFormsWithoutResolution()
    {
        var workflow = CreateWorkflow(
            Loop("input", new InputValueDefinition()),
            Loop("output", new CurrentOutputValueDefinition()),
            Loop("context", new ContextItemValueDefinition { Key = "context-key" }),
            Loop("literal", new LiteralValueDefinition { Value = 42L }));

        var restored = Restore(Map(workflow));
        var loops = restored.Options.Steps.Cast<LoopStepDefinition>().ToArray();

        loops[0].Items.Should().BeOfType<InputValueDefinition>();
        loops[1].Items.Should().BeOfType<CurrentOutputValueDefinition>();
        loops[2].Items.Should().BeOfType<ContextItemValueDefinition>()
            .Which.Key.Should().Be("context-key");
        loops[3].Items.Should().BeOfType<LiteralValueDefinition>()
            .Which.Value.Should().BeOfType<long>().Which.Should().Be(42L);
    }

    [Fact]
    public void FromDocument_ShouldReconstructCanonicalLiteralClrTree()
    {
        object?[] value =
        [
            null,
            "text",
            true,
            42L,
            12.5m,
            new object?[] { 1L, "nested" },
            new Dictionary<string, object?>
            {
                ["z"] = 3L,
                ["A"] = 1L,
                ["a"] = 2L
            }
        ];

        var restored = Restore(Map(CreateWorkflow(Loop("literal", new LiteralValueDefinition { Value = value }))));
        var literal = restored.Options.Steps.Single()
            .Should().BeOfType<LoopStepDefinition>().Subject.Items
            .Should().BeOfType<LiteralValueDefinition>().Subject;
        var values = literal.Value.Should().BeAssignableTo<IReadOnlyList<object?>>().Subject;

        values[0].Should().BeNull();
        values[1].Should().BeOfType<string>().Which.Should().Be("text");
        values[2].Should().BeOfType<bool>().Which.Should().BeTrue();
        values[3].Should().BeOfType<long>().Which.Should().Be(42L);
        values[4].Should().BeOfType<decimal>().Which.Should().Be(12.5m);
        values[5].Should().BeAssignableTo<IReadOnlyList<object?>>();

        var map = values[6]
            .Should().BeAssignableTo<IReadOnlyDictionary<string, object?>>().Subject;
        map.Should().BeOfType<ReadOnlyDictionary<string, object?>>();
        map.Keys.Should().Equal("A", "a", "z");
    }

    [Fact]
    public void FromDocument_ShouldDeriveExactReferencesFromReconstructedRunSteps()
    {
        var first = AgentReference("1.0.0");
        var second = AgentReference("2.0.0");
        var workflow = CreateWorkflow(
            new RunStepDefinition { Agent = first },
            new ParallelStepDefinition
            {
                Name = "nested",
                Steps =
                [
                    new RunStepDefinition { Agent = first },
                    new RunStepDefinition { Agent = second }
                ]
            });

        var document = Map(workflow);
        var restored = Restore(document);

        restored.References.Should().HaveCount(2);
        restored.References.Should().Equal(first, second);
        restored.References.Should().Equal(
            document.References.Select(reference => new AssetReference(
                reference.AssetType == AIAssetDocumentType.Agent
                    ? AssetType.Agent
                    : throw new InvalidOperationException(),
                new AssetId(Guid.Parse(reference.AssetId)),
                new AssetUrn(reference.Urn),
                new AssetVersion(reference.Version))));
    }

    [Fact]
    public void FromDocument_ShouldRestorePersistedAssetStateAndCanonicalWorkflowMetadata()
    {
        var dependencyReference = new AssetReference(
            AssetType.Policy,
            AssetId.New(),
            new AssetUrn("urn:pulsestack:policy:required"),
            new AssetVersion("3.0.0"));
        var seed = CreateWorkflow();
        var source = seed with
        {
            Version = new AssetVersion("2.3.4"),
            Lifecycle = AssetLifecycle.Published,
            Metadata = seed.Metadata with
            {
                Name = "Workflow",
                Description = "Description",
                Author = "PulseStackAI",
                Category = "Business",
                Tags = ["portable", "workflow"]
            },
            Dependencies = [new AssetDependency(dependencyReference)]
        };

        var restored = Restore(Map(source));

        restored.Id.Should().Be(source.Id);
        restored.Urn.Should().Be(source.Urn);
        restored.Version.Should().Be(new AssetVersion("2.3.4"));
        restored.Lifecycle.Should().Be(AssetLifecycle.Published);
        restored.Metadata.Should().BeEquivalentTo(source.Metadata);
        restored.Metadata.Tags.Should().Equal("portable", "workflow");
        restored.Dependencies.Should().Equal(source.Dependencies);
        restored.Options.Name.Should().Be(restored.Metadata.Name);
        restored.Options.Description.Should().Be(restored.Metadata.Description);
    }

    [Fact]
    public void FromDocument_ShouldRejectNonCanonicalWorkflowStepIdDefensively()
    {
        var source = Map(CreateWorkflow(new ParallelStepDefinition { Name = "root", Steps = [] }));
        var original = source.Steps.Single().Should().BeOfType<ParallelStepDocument>().Subject;
        var malformed = new ParallelStepDocument(
            original.StepId.ToUpperInvariant(),
            original.Name,
            original.Steps);
        var document = new WorkflowAssetDocument(
            source.SchemaVersion,
            source.Identity,
            source.Metadata,
            source.Lifecycle,
            [malformed],
            source.References,
            source.Dependencies);

        var act = () => new AIAssetDocumentMapper().FromDocument(document);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*$.steps[0].stepId*canonical lowercase non-empty GUID in D format*");
    }

    [Fact]
    public void FromDocument_ShouldRejectUnsupportedWorkflowAgentReferenceTypeDefensively()
    {
        var source = Map(CreateWorkflow(new RunStepDefinition { Agent = AgentReference() }));
        var run = source.Steps.Single().Should().BeOfType<RunStepDocument>().Subject;
        var invalidReference = run.Agent with
        {
            AssetType = (AIAssetDocumentType)999
        };
        var document = new WorkflowAssetDocument(
            source.SchemaVersion,
            source.Identity,
            source.Metadata,
            source.Lifecycle,
            [new RunStepDocument(run.StepId, invalidReference)],
            [],
            source.Dependencies);

        var act = () => new AIAssetDocumentMapper().FromDocument(document);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void FromDocument_ThenToDocument_ShouldPreserveCanonicalDocumentStructuralEquality()
    {
        var nestedLiteral = new Dictionary<string, object?>
        {
            ["z"] = new object?[] { 2L, false },
            ["A"] = null,
            ["a"] = 1.5m
        };
        var agent = AgentReference("4.2.0");
        var seed = CreateWorkflow(
            new ParallelStepDefinition
            {
                Name = "parallel",
                Steps =
                [
                    new RunStepDefinition { Agent = agent },
                    new ConditionalStepDefinition
                    {
                        Name = "if",
                        Condition = new NamedConditionDefinition { Name = "ready" },
                        ThenStep = Loop("literal", new LiteralValueDefinition { Value = nestedLiteral }),
                        ElseStep = new SwitchStepDefinition
                        {
                            Name = "switch",
                            Selector = new InputValueDefinition(),
                            Cases =
                            [
                                new SwitchCaseDefinition
                                {
                                    Value = "retry",
                                    Step = new RetryStepDefinition
                                    {
                                        Name = "retry",
                                        MaxAttempts = 5,
                                        Step = new RunStepDefinition { Agent = agent }
                                    }
                                }
                            ]
                        }
                    }
                ]
            });
        var source = seed with
        {
            Version = new AssetVersion("9.1.0"),
            Lifecycle = AssetLifecycle.Validated,
            Metadata = seed.Metadata with
            {
                Name = "Workflow",
                Description = "Description",
                Author = "PulseStackAI",
                Category = "Conformance",
                Tags = ["roundtrip"]
            }
        };
        var original = Map(source);

        var restored = new AIAssetDocumentMapper().FromDocument(original);
        var roundTripped = new AIAssetDocumentMapper().ToDocument(restored)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

        roundTripped.Should().Be(original);
    }

    private static LoopStepDefinition Loop(string name, WorkflowValueDefinition value)
        => new()
        {
            Name = name,
            Items = value,
            Step = new ParallelStepDefinition
            {
                Name = $"{name}-body",
                Steps = []
            }
        };

    private static WorkflowAssetDocument Map(WorkflowAsset workflow)
        => new AIAssetDocumentMapper().ToDocument(workflow)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

    private static WorkflowAsset Restore(WorkflowAssetDocument document)
        => new AIAssetDocumentMapper().FromDocument(document)
            .Should().BeOfType<WorkflowAsset>().Subject;

    private static WorkflowAsset CreateWorkflow(params WorkflowStepDefinition[] steps)
        => new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Workflow",
                Description = "Description",
                Steps = steps
            });

    private static AssetReference AgentReference(string version = "1.0.0")
    {
        var id = AssetId.New();
        return new AssetReference(
            AssetType.Agent,
            id,
            new AssetUrn($"urn:pulsestack:agent:{id}"),
            new AssetVersion(version));
    }
}
