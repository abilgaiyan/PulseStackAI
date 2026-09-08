using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Documents.Workflows;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class WorkflowDocumentVocabularyTests
{
    [Fact]
    public void StepDocuments_ShouldUseTheirFixedDiscriminatorsAndPreservePayloads()
    {
        var agent = CreateAgentReference();
        var run = new RunStepDocument(Id(1), agent);
        var parallelChildren = new List<WorkflowStepDocument> { run };
        var parallel = new ParallelStepDocument(Id(2), "Parallel", parallelChildren);
        var condition = new NamedConditionDocument("is-approved");
        var conditional = new ConditionalStepDocument(Id(3), "Conditional", condition, run, parallel);
        var retry = new RetryStepDocument(Id(4), "Retry", conditional, 3);
        var loop = new LoopStepDocument(Id(5), "Loop", new InputValueDocument(), retry);
        var cases = new List<SwitchCaseDocument> { new("Approve", loop) };
        var @switch = new SwitchStepDocument(Id(6), "Switch", new CurrentOutputValueDocument(), cases, run);

        run.Kind.Should().Be(WorkflowStepDocumentKind.Run);
        run.StepId.Should().Be(Id(1));
        run.Agent.Should().Be(agent);

        parallel.Kind.Should().Be(WorkflowStepDocumentKind.Parallel);
        parallel.StepId.Should().Be(Id(2));
        parallel.Name.Should().Be("Parallel");
        parallel.Steps.Should().Equal(run);

        conditional.Kind.Should().Be(WorkflowStepDocumentKind.Conditional);
        conditional.StepId.Should().Be(Id(3));
        conditional.Name.Should().Be("Conditional");
        conditional.Condition.Should().Be(condition);
        conditional.ThenStep.Should().Be(run);
        conditional.ElseStep.Should().Be(parallel);

        retry.Kind.Should().Be(WorkflowStepDocumentKind.Retry);
        retry.StepId.Should().Be(Id(4));
        retry.Name.Should().Be("Retry");
        retry.Step.Should().Be(conditional);
        retry.MaxAttempts.Should().Be(3);

        loop.Kind.Should().Be(WorkflowStepDocumentKind.Loop);
        loop.StepId.Should().Be(Id(5));
        loop.Name.Should().Be("Loop");
        loop.Items.Should().BeOfType<InputValueDocument>();
        loop.Step.Should().Be(retry);

        @switch.Kind.Should().Be(WorkflowStepDocumentKind.Switch);
        @switch.StepId.Should().Be(Id(6));
        @switch.Name.Should().Be("Switch");
        @switch.Selector.Should().BeOfType<CurrentOutputValueDocument>();
        @switch.Cases.Should().Equal(cases);
        @switch.DefaultStep.Should().Be(run);
    }

    [Fact]
    public void WorkflowRootAndNamedCondition_ShouldUseFixedDiscriminatorsAndPreserveSymbolicState()
    {
        var workflow = CreateWorkflow([]);
        var condition = new NamedConditionDocument("inventory-available");

        workflow.AssetType.Should().Be(AIAssetDocumentType.Workflow);
        condition.Kind.Should().Be(WorkflowConditionDocumentKind.Named);
        condition.Name.Should().Be("inventory-available");
    }

    [Fact]
    public void ValueDocuments_ShouldUseTheirFixedDiscriminatorsAndPreserveSemantics()
    {
        var input = new InputValueDocument();
        var currentOutput = new CurrentOutputValueDocument();
        var contextItem = new ContextItemValueDocument("order-id");
        var literal = new LiteralValueDocument(new StringWorkflowLiteralDocument("ready"));

        input.Kind.Should().Be(WorkflowValueDocumentKind.Input);
        currentOutput.Kind.Should().Be(WorkflowValueDocumentKind.CurrentOutput);
        contextItem.Kind.Should().Be(WorkflowValueDocumentKind.ContextItem);
        contextItem.Key.Should().Be("order-id");
        literal.Kind.Should().Be(WorkflowValueDocumentKind.Literal);
        literal.Literal.Should().Be(new StringWorkflowLiteralDocument("ready"));
    }

    [Fact]
    public void LiteralDocuments_ShouldUseTheirFixedDiscriminatorsAndPreserveTypedPayloads()
    {
        var array = new ArrayWorkflowLiteralDocument(
        [
            new StringWorkflowLiteralDocument("a"),
            new IntegerWorkflowLiteralDocument(2)
        ]);
        var obj = new ObjectWorkflowLiteralDocument(
        [
            new WorkflowLiteralPropertyDocument("enabled", new BooleanWorkflowLiteralDocument(true))
        ]);

        new NullWorkflowLiteralDocument().Kind.Should().Be(WorkflowLiteralDocumentKind.Null);

        var text = new StringWorkflowLiteralDocument("text");
        text.Kind.Should().Be(WorkflowLiteralDocumentKind.String);
        text.Value.Should().Be("text");

        var boolean = new BooleanWorkflowLiteralDocument(true);
        boolean.Kind.Should().Be(WorkflowLiteralDocumentKind.Boolean);
        boolean.Value.Should().BeTrue();

        var integer = new IntegerWorkflowLiteralDocument(long.MaxValue);
        integer.Kind.Should().Be(WorkflowLiteralDocumentKind.Integer);
        integer.Value.Should().Be(long.MaxValue);

        var decimalValue = new DecimalWorkflowLiteralDocument(123.45m);
        decimalValue.Kind.Should().Be(WorkflowLiteralDocumentKind.Decimal);
        decimalValue.Value.Should().Be(123.45m);

        array.Kind.Should().Be(WorkflowLiteralDocumentKind.Array);
        array.Items.Should().Equal(
            new StringWorkflowLiteralDocument("a"),
            new IntegerWorkflowLiteralDocument(2));

        obj.Kind.Should().Be(WorkflowLiteralDocumentKind.Object);
        obj.Properties.Should().Equal(
            new WorkflowLiteralPropertyDocument("enabled", new BooleanWorkflowLiteralDocument(true)));
    }

    [Fact]
    public void CollectionBearingDocuments_ShouldSnapshotSourcesWithoutChangingEqualityOrHashes()
    {
        var run = CreateRun(1);

        var rootSource = new List<WorkflowStepDocument> { run };
        var root = CreateWorkflow(rootSource);
        var rootEquivalent = CreateWorkflow([run]);
        var rootHash = root.GetHashCode();

        var parallelSource = new List<WorkflowStepDocument> { run };
        var parallel = new ParallelStepDocument(Id(2), "Parallel", parallelSource);
        var parallelEquivalent = new ParallelStepDocument(Id(2), "Parallel", [run]);
        var parallelHash = parallel.GetHashCode();

        var switchSource = new List<SwitchCaseDocument> { new("A", run) };
        var @switch = new SwitchStepDocument(Id(3), "Switch", new InputValueDocument(), switchSource);
        var switchEquivalent = new SwitchStepDocument(
            Id(3),
            "Switch",
            new InputValueDocument(),
            [new SwitchCaseDocument("A", run)]);
        var switchHash = @switch.GetHashCode();

        var arraySource = new List<WorkflowLiteralDocument> { new StringWorkflowLiteralDocument("A") };
        var array = new ArrayWorkflowLiteralDocument(arraySource);
        var arrayEquivalent = new ArrayWorkflowLiteralDocument([new StringWorkflowLiteralDocument("A")]);
        var arrayHash = array.GetHashCode();

        var objectSource = new List<WorkflowLiteralPropertyDocument>
        {
            new("a", new IntegerWorkflowLiteralDocument(1))
        };
        var obj = new ObjectWorkflowLiteralDocument(objectSource);
        var objectEquivalent = new ObjectWorkflowLiteralDocument(
        [
            new WorkflowLiteralPropertyDocument("a", new IntegerWorkflowLiteralDocument(1))
        ]);
        var objectHash = obj.GetHashCode();

        rootSource.Add(CreateRun(10));
        parallelSource.Add(CreateRun(11));
        switchSource.Add(new SwitchCaseDocument("B", CreateRun(12)));
        arraySource.Add(new StringWorkflowLiteralDocument("B"));
        objectSource.Add(new WorkflowLiteralPropertyDocument("b", new IntegerWorkflowLiteralDocument(2)));

        root.Should().Be(rootEquivalent);
        root.GetHashCode().Should().Be(rootHash);
        parallel.Should().Be(parallelEquivalent);
        parallel.GetHashCode().Should().Be(parallelHash);
        @switch.Should().Be(switchEquivalent);
        @switch.GetHashCode().Should().Be(switchHash);
        array.Should().Be(arrayEquivalent);
        array.GetHashCode().Should().Be(arrayHash);
        obj.Should().Be(objectEquivalent);
        obj.GetHashCode().Should().Be(objectHash);
    }

    [Fact]
    public void IndependentlyConstructedEquivalentDeepTrees_ShouldBeEqualAndHaveEqualHashes()
    {
        var first = CreateRepresentativeWorkflow();
        var second = CreateRepresentativeWorkflow();

        first.Should().Be(second);
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void OrderingDifferences_ShouldProduceInequalityWhereOrderingIsStructural()
    {
        var firstRun = CreateRun(1);
        var secondRun = CreateRun(2);

        CreateWorkflow([firstRun, secondRun])
            .Should().NotBe(CreateWorkflow([secondRun, firstRun]));

        new ParallelStepDocument(Id(3), "Parallel", [firstRun, secondRun])
            .Should().NotBe(new ParallelStepDocument(Id(3), "Parallel", [secondRun, firstRun]));

        new SwitchStepDocument(
                Id(4),
                "Switch",
                new InputValueDocument(),
                [new SwitchCaseDocument("A", firstRun), new SwitchCaseDocument("B", secondRun)])
            .Should().NotBe(new SwitchStepDocument(
                Id(4),
                "Switch",
                new InputValueDocument(),
                [new SwitchCaseDocument("B", secondRun), new SwitchCaseDocument("A", firstRun)]));

        new ArrayWorkflowLiteralDocument(
                [new StringWorkflowLiteralDocument("a"), new StringWorkflowLiteralDocument("b")])
            .Should().NotBe(new ArrayWorkflowLiteralDocument(
                [new StringWorkflowLiteralDocument("b"), new StringWorkflowLiteralDocument("a")]));

        new ObjectWorkflowLiteralDocument(
                [
                    new WorkflowLiteralPropertyDocument("a", new IntegerWorkflowLiteralDocument(1)),
                    new WorkflowLiteralPropertyDocument("b", new IntegerWorkflowLiteralDocument(2))
                ])
            .Should().NotBe(new ObjectWorkflowLiteralDocument(
                [
                    new WorkflowLiteralPropertyDocument("b", new IntegerWorkflowLiteralDocument(2)),
                    new WorkflowLiteralPropertyDocument("a", new IntegerWorkflowLiteralDocument(1))
                ]));
    }

    private static WorkflowAssetDocument CreateRepresentativeWorkflow()
    {
        var run1 = CreateRun(1);
        var run2 = CreateRun(2);
        var run3 = CreateRun(3);
        var run4 = CreateRun(4);
        var run5 = CreateRun(5);

        var conditional = new ConditionalStepDocument(
            Id(20),
            "Conditional",
            new NamedConditionDocument("approved"),
            run2,
            run3);

        var retry = new RetryStepDocument(Id(21), "Retry", run4, 3);

        var loop = new LoopStepDocument(
            Id(22),
            "Loop",
            new LiteralValueDocument(
                new ArrayWorkflowLiteralDocument(
                [
                    new StringWorkflowLiteralDocument("north"),
                    new StringWorkflowLiteralDocument("south")
                ])),
            retry);

        var @switch = new SwitchStepDocument(
            Id(23),
            "Switch",
            new ContextItemValueDocument("decision"),
            [
                new SwitchCaseDocument("Approve", loop),
                new SwitchCaseDocument("Reject", run5)
            ],
            run1);

        var parallel = new ParallelStepDocument(
            Id(24),
            "Parallel",
            [conditional, @switch]);

        return CreateWorkflow([parallel]);
    }

    private static WorkflowAssetDocument CreateWorkflow(IEnumerable<WorkflowStepDocument> steps)
        => new(
            AIAssetSchemaVersion.V1,
            new AIAssetIdentityDocument
            {
                Id = "11111111-1111-1111-1111-111111111111",
                Urn = "urn:pulsestack:test:workflow",
                Version = "1.0.0"
            },
            new AIAssetMetadataDocument("Workflow", description: "Vocabulary proof"),
            AIAssetLifecycleDocument.Draft,
            steps);

    private static RunStepDocument CreateRun(int id)
        => new(Id(id), CreateAgentReference());

    private static AIAssetReferenceDocument CreateAgentReference()
        => new()
        {
            AssetType = AIAssetDocumentType.Agent,
            AssetId = "22222222-2222-2222-2222-222222222222",
            Urn = "urn:pulsestack:test:agent",
            Version = "1.0.0"
        };

    private static string Id(int value)
        => $"00000000-0000-0000-0000-{value:000000000000}";
}
