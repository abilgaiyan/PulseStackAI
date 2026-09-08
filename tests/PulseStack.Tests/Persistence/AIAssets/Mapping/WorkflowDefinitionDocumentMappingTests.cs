using System.Collections;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Workflows.Definitions;
using PulseStack.Abstractions.Workflows.Values;
using PulseStack.Core.Assets;
using PulseStack.Core.Persistence.AIAssets.Mapping;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets.Mapping;

public sealed class WorkflowDefinitionDocumentMappingTests
{
    [Fact]
    public void ToDocument_ShouldMapCompleteRecursiveWorkflowGrammarInAuthoredOrder()
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

        var workflow = CreateWorkflow(parallel);

        var document = new AIAssetDocumentMapper().ToDocument(workflow)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

        document.Steps.Should().ContainSingle();
        var parallelDocument = document.Steps[0]
            .Should().BeOfType<ParallelStepDocument>().Subject;
        parallelDocument.StepId.Should().Be(parallel.Id.Value.ToString("D"));
        parallelDocument.Steps.Should().HaveCount(2);
        parallelDocument.Steps[0].Should().BeOfType<RunStepDocument>();

        var switchDocument = parallelDocument.Steps[1]
            .Should().BeOfType<SwitchStepDocument>().Subject;
        switchDocument.Selector.Should().BeOfType<CurrentOutputValueDocument>();
        switchDocument.Cases.Should().ContainSingle();

        var conditionalDocument = switchDocument.Cases[0].Step
            .Should().BeOfType<ConditionalStepDocument>().Subject;
        conditionalDocument.Condition.Should().BeOfType<NamedConditionDocument>()
            .Which.Name.Should().Be("should-run");

        var loopDocument = conditionalDocument.ThenStep
            .Should().BeOfType<LoopStepDocument>().Subject;
        loopDocument.Items.Should().BeOfType<ContextItemValueDocument>()
            .Which.Key.Should().Be("items");

        var retryDocument = loopDocument.Step
            .Should().BeOfType<RetryStepDocument>().Subject;
        retryDocument.MaxAttempts.Should().Be(4);
        retryDocument.Step.Should().BeOfType<RunStepDocument>();
        conditionalDocument.ElseStep.Should().BeOfType<RunStepDocument>();
        switchDocument.DefaultStep.Should().BeOfType<RunStepDocument>();

        document.References.Should().ContainSingle();
        document.References[0].AssetId.Should().Be(agent.Id.ToString());
        document.References[0].Urn.Should().Be(agent.Urn.Value);
        document.References[0].Version.Should().Be(agent.Version.Value);
    }

    [Fact]
    public void ToDocument_ShouldPreserveCommonEnvelopeStateAndCanonicalWorkflowMetadata()
    {
        var dependencyReference = new AssetReference(
            AssetType.Policy,
            AssetId.New(),
            new AssetUrn("urn:pulsestack:policy:required"),
            AssetVersion.Initial);
        var workflow = CreateWorkflow() with
        {
            Version = new AssetVersion("2.0"),
            Lifecycle = AssetLifecycle.Published,
            Metadata = CreateWorkflow().Metadata with
            {
                Name = "Workflow",
                Description = "Description",
                Author = "PulseStackAI",
                Category = "Business"
            },
            Dependencies = [new AssetDependency(dependencyReference)]
        };

        var document = new AIAssetDocumentMapper().ToDocument(workflow)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;

        document.Identity.Id.Should().Be(workflow.Id.ToString());
        document.Identity.Urn.Should().Be(workflow.Urn.Value);
        document.Identity.Version.Should().Be("2.0");
        document.Metadata.Name.Should().Be("Workflow");
        document.Metadata.Description.Should().Be("Description");
        document.Metadata.Author.Should().Be("PulseStackAI");
        document.Metadata.Category.Should().Be("Business");
        document.Dependencies.Should().ContainSingle();
    }

    [Fact]
    public void ToDocument_ShouldRejectWorkflowMetadataThatDisagreesWithOptions()
    {
        var workflow = CreateWorkflow() with
        {
            Metadata = CreateWorkflow().Metadata with
            {
                Name = "Different"
            }
        };

        var act = () => new AIAssetDocumentMapper().ToDocument(workflow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Workflow Asset options field 'Name' does not match canonical Metadata*");
    }

    [Fact]
    public void ToDocument_ShouldNormalizeSupportedScalarLiterals()
    {
        object?[] values =
        [
            null,
            "value",
            true,
            (byte)1,
            (sbyte)-2,
            (short)-3,
            (ushort)4,
            -5,
            (uint)6,
            (long)-7,
            (ulong)8,
            9.5m
        ];

        var document = MapLiteral(values);
        var array = document.Should().BeOfType<ArrayWorkflowLiteralDocument>().Subject;

        array.Items.Should().HaveCount(values.Length);
        array.Items[0].Should().BeOfType<NullWorkflowLiteralDocument>();
        array.Items[1].Should().BeOfType<StringWorkflowLiteralDocument>()
            .Which.Value.Should().Be("value");
        array.Items[2].Should().BeOfType<BooleanWorkflowLiteralDocument>()
            .Which.Value.Should().BeTrue();
        array.Items.Skip(3).Take(8)
            .Should().AllSatisfy(item => item.Should().BeOfType<IntegerWorkflowLiteralDocument>());
        array.Items[^1].Should().BeOfType<DecimalWorkflowLiteralDocument>()
            .Which.Value.Should().Be(9.5m);
    }

    [Fact]
    public void ToDocument_ShouldCanonicalizeMapBeforeSequenceAndSortKeysOrdinally()
    {
        var value = new Dictionary<string, object?>
        {
            ["z"] = 2,
            ["A"] = 3,
            ["a"] = 1
        };

        var literal = MapLiteral(value)
            .Should().BeOfType<ObjectWorkflowLiteralDocument>().Subject;

        literal.Properties.Select(property => property.Name)
            .Should().Equal("A", "a", "z");
        literal.Properties.Should().AllSatisfy(property =>
            property.Value.Should().BeOfType<IntegerWorkflowLiteralDocument>());
    }

    [Fact]
    public void ToDocument_ShouldUseCountAndIndexerForSupportedOrderedLists()
    {
        var value = new ThrowingEnumerableReadOnlyList("first", "second");

        var literal = MapLiteral(value)
            .Should().BeOfType<ArrayWorkflowLiteralDocument>().Subject;

        literal.Items.Should().HaveCount(2);
        literal.Items[0].Should().BeOfType<StringWorkflowLiteralDocument>()
            .Which.Value.Should().Be("first");
        literal.Items[1].Should().BeOfType<StringWorkflowLiteralDocument>()
            .Which.Value.Should().Be("second");
    }

    [Fact]
    public void ToDocument_ShouldAllowRepeatedAcyclicCollectionInstances()
    {
        var shared = new List<object?> { 1, 2 };
        object?[] root = [shared, shared];

        var literal = MapLiteral(root)
            .Should().BeOfType<ArrayWorkflowLiteralDocument>().Subject;

        literal.Items.Should().HaveCount(2);
        literal.Items.Should().AllSatisfy(item =>
            item.Should().BeOfType<ArrayWorkflowLiteralDocument>());
    }

    [Fact]
    public void ToDocument_ShouldRejectCyclicCollectionGraphWithSemanticPath()
    {
        var cycle = new List<object?>();
        cycle.Add(cycle);

        var act = () => MapLiteral(cycle);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*$.steps[0].items.literal.items[0]*Cyclic workflow literal collection graph*");
    }

    [Theory]
    [MemberData(nameof(UnsupportedLiteralValues))]
    public void ToDocument_ShouldRejectUnsupportedLiteralValues(object value)
    {
        var act = () => MapLiteral(value);

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*$.steps[0].items.literal*");
    }

    [Fact]
    public void ToDocument_ShouldRejectMapWhoseAdvertisedCountDoesNotMatchEmission()
    {
        var act = () => MapLiteral(new InconsistentReadOnlyMap());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*advertised Count 2 but emitted 1 entries*");
    }

    [Fact]
    public void ToDocument_ShouldRejectMapThatEmitsDuplicateOrdinalKey()
    {
        var act = () => MapLiteral(new DuplicateKeyReadOnlyMap());

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*duplicate key 'same' under ordinal comparison*");
    }

    public static IEnumerable<object[]> UnsupportedLiteralValues()
    {
        yield return [1.25f];
        yield return [1.25d];
        yield return [ulong.MaxValue];
        yield return [new HashSet<int> { 1, 2 }];
        yield return [Enumerable.Range(1, 3).Where(static value => value > 0)];
        yield return [new object()];
        yield return [new int[1, 1]];
        yield return [Array.CreateInstance(typeof(int), [1], [1])];
    }

    private static WorkflowLiteralDocument MapLiteral(object? value)
    {
        var workflow = CreateWorkflow(
            new LoopStepDefinition
            {
                Name = "literal-loop",
                Items = new LiteralValueDefinition { Value = value },
                Step = new ParallelStepDefinition
                {
                    Name = "empty",
                    Steps = []
                }
            });

        var document = new AIAssetDocumentMapper().ToDocument(workflow)
            .Should().BeOfType<WorkflowAssetDocument>().Subject;
        return document.Steps[0]
            .Should().BeOfType<LoopStepDocument>().Subject.Items
            .Should().BeOfType<LiteralValueDocument>().Subject.Literal;
    }

    private static WorkflowAsset CreateWorkflow(params WorkflowStepDefinition[] steps)
        => new WorkflowAssetFactory().Create(
            new WorkflowAssetOptions
            {
                Name = "Workflow",
                Description = "Description",
                Steps = steps
            });

    private static AssetReference AgentReference()
    {
        var id = AssetId.New();
        return new AssetReference(
            AssetType.Agent,
            id,
            new AssetUrn($"urn:pulsestack:agent:{id}"),
            AssetVersion.Initial);
    }

    private sealed class ThrowingEnumerableReadOnlyList(params object?[] items)
        : IReadOnlyList<object?>
    {
        public int Count => items.Length;

        public object? this[int index] => items[index];

        public IEnumerator<object?> GetEnumerator()
            => throw new InvalidOperationException("Enumeration must not be used.");

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    private sealed class InconsistentReadOnlyMap : IReadOnlyDictionary<string, object?>
    {
        public int Count => 2;

        public IEnumerable<string> Keys => ["only"];

        public IEnumerable<object?> Values => [1];

        public object? this[string key] => 1;

        public bool ContainsKey(string key) => key == "only";

        public bool TryGetValue(string key, out object? value)
        {
            value = 1;
            return key == "only";
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object?>("only", 1);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class DuplicateKeyReadOnlyMap : IReadOnlyDictionary<string, object?>
    {
        public int Count => 2;

        public IEnumerable<string> Keys => ["same", "same"];

        public IEnumerable<object?> Values => [1, 2];

        public object? this[string key] => 1;

        public bool ContainsKey(string key) => key == "same";

        public bool TryGetValue(string key, out object? value)
        {
            value = 1;
            return key == "same";
        }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return new KeyValuePair<string, object?>("same", 1);
            yield return new KeyValuePair<string, object?>("same", 2);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
