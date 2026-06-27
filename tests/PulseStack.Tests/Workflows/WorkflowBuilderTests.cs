using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowBuilderTests
{
    [Fact]
    public void WorkflowBuilder_Should_Create_Workflow()
    {
        var builder =
            Workflow.Create(
                "Research");

        var workflow =
            builder.Build();

        workflow.Name.Should().Be("Research");
        workflow.Nodes.Should().BeEmpty();
    }

    [Fact]
    public void WorkflowBuilder_Should_Add_Agent()
    {
        var agent =
            new FakeAgent(
                "Researcher",
                "Done");

        var workflow =
            Workflow.Create(
                    "Research")
                .Run(agent)
                .Build();

        workflow.Nodes.Should().ContainSingle();
        workflow.Nodes.Single().Should().BeSameAs(agent);
    }

    [Fact]
    public void WorkflowBuilder_Should_Add_Nested_Workflow()
    {
        var childWorkflow =
            new WorkflowPipeline(
                "Child");

        var workflow =
            Workflow.Create(
                    "Parent")
                .Workflow(childWorkflow)
                .Build();

        workflow.Nodes.Should().ContainSingle();
        workflow.Nodes.Single().Should().BeSameAs(childWorkflow);
    }

    [Fact]
    public void WorkflowBuilder_Should_Return_WorkflowPipeline()
    {
        var workflow =
            Workflow.Create(
                    "Research")
                .Build();

        workflow.Should().BeOfType<WorkflowPipeline>();
    }

    [Fact]
    public void Build_Should_Return_Same_Workflow()
    {

        var builder =
            Workflow.Create("Research");

        var workflow1 =
            builder.Build();

        var workflow2 =
            builder.Build();

        workflow1.Should().BeSameAs(workflow2);
    }

    [Fact]
    public void Builder_Should_Support_Chaining()
    {
        var workflow =
            Workflow.Create("Research")
                .Run(new FakeAgent("A", "Done"))
                .Run(new FakeAgent("B", "Done"))
                .Build();

        workflow.Nodes.Should().HaveCount(2);
    }

    [Fact]
    public void Workflow_Create_Should_Throw_When_Name_Is_Empty()
    {
        Action action =
            () => Workflow.Create("");

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void Workflow_Create_Should_Throw_When_Name_Is_Null()
    {
        Action action =
            () => Workflow.Create(null!);

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Condition_Is_Null()
    {
        Action action = () => Workflow.Create("Test")
            .If(null!, new FakeAgent("Test", "Done"));

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Node_Is_Null()
    {
        Action action = () => Workflow.Create("Test")
            .If(new DelegateCondition(_ => true), null!);

        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Use_Default_Name()
    {
        var condition = new DelegateCondition(_ => true);
        var thenNode = new FakeAgent("ApprovalStep", "Approved");

        var workflow = Workflow.Create("Research")
            .Run(new FakeAgent("ResearchStep", "Done"))
            .If(condition, thenNode)           // overload without name
            .Build();

        var conditional = workflow.Nodes.OfType<ConditionalNode>().Single();
        conditional.Name.Should().Be("If");
        conditional.Condition.Should().BeSameAs(condition);
        conditional.Node.Should().BeSameAs(thenNode);
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Use_Custom_Name()
    {
        var condition = new DelegateCondition(_ => true);
        var thenNode = new FakeAgent("ApprovalStep", "Approved");

        var workflow = Workflow.Create("Research")
            .If("IsUserApproved", condition, thenNode)   // overload with name
            .Build();

        var conditional = workflow.Nodes.OfType<ConditionalNode>().Single();
        conditional.Name.Should().Be("IsUserApproved");
    }

    [Fact]
    public void WorkflowBuilder_Should_Support_Chaining_Both_Overloads()
    {
        var workflow = Workflow.Create("Test")
            .If(new DelegateCondition(_ => true), new FakeAgent("If1", "Path1"))
            .If("CheckPermission", new DelegateCondition(_ => false), new FakeAgent("If2", "Path2"))
            .Build();

        workflow.Nodes.OfType<ConditionalNode>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Name_Is_Empty()
    {
        Action action =
            () => Workflow.Create("Test")
                .If(
                    "",
                    new DelegateCondition(_ => true),
                    new FakeAgent("A", "Done"));

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_If_Should_Throw_When_Name_Is_Null()
    {
        Action action =
            () => Workflow.Create("Test")
                .If(
                    null!,
                    new DelegateCondition(_ => true),
                    new FakeAgent("A", "Done"));

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Use_Default_Name()
    {
        var agent = new FakeAgent("ValidationStep", "Done");

        var workflow = Workflow.Create("Research")
            .Retry(agent, 3)
            .Build();

        var retryNode = workflow.Nodes.OfType<RetryNode>().Single();

        retryNode.Name.Should().Be("Retry");
        retryNode.MaxAttempts.Should().Be(3);
        retryNode.Node.Should().BeSameAs(agent);
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Use_Custom_Name()
    {
        var agent = new FakeAgent("ValidationStep", "Done");

        var workflow = Workflow.Create("Research")
            .Retry("Retry Validation", agent, 5)
            .Build();

        var retryNode = workflow.Nodes.OfType<RetryNode>().Single();

        retryNode.Name.Should().Be("Retry Validation");
        retryNode.MaxAttempts.Should().Be(5);
        retryNode.Node.Should().BeSameAs(agent);
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Support_Chaining_Both_Overloads()
    {
        var workflow = Workflow.Create("Test")
            .Retry(new FakeAgent("A", "Done"), 2)
            .Retry("CustomRetry", new FakeAgent("B", "Done"), 4)
            .Build();

        workflow.Nodes.OfType<RetryNode>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_Node_Is_Null()
    {
        Action action = () => Workflow.Create("Test").Retry(null!, 3);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_Name_Is_Empty()
    {
        Action action = () => Workflow.Create("Test").Retry("", new FakeAgent("A", "Done"), 3);
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_MaxAttempts_Is_Zero()
    {
        Action action = () => Workflow.Create("Test").Retry(new FakeAgent("A", "Done"), 0);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_MaxAttempts_Is_Negative()
    {
        Action action = () => Workflow.Create("Test").Retry(new FakeAgent("A", "Done"), -1);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Use_Default_MaxAttempts()
    {
        var workflow =
            Workflow.Create("Test")
                .Retry(
                    new FakeAgent("A", "Done"))
                .Build();

        var retry =
            workflow.Nodes
                .OfType<RetryNode>()
                .Single();

        retry.MaxAttempts.Should().Be(3);
    }

    [Fact]
    public void WorkflowBuilder_Retry_Should_Throw_When_Name_Is_Null()
    {
         Action action =
            () => Workflow.Create("Test")
                .Retry(null!,
                    new FakeAgent("A", "Done"));

        action.Should()
            .Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Use_Default_Name()
    {
        Func<PipelineContext, IEnumerable<object>> itemsSelector = 
            _ => [1, 2, 3 ];

        var processor = new FakeAgent("ProcessItem", "Processed");

        var workflow = Workflow.Create("Research")
            .ForEach(itemsSelector, processor)
            .Build();

        var loopNode = workflow.Nodes.OfType<LoopNode>().Single();

        loopNode.Name.Should().Be("ForEach");
        loopNode.Items.Should().BeSameAs(itemsSelector);
        loopNode.Node.Should().BeSameAs(processor);
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Use_Custom_Name()
    {
        Func<PipelineContext, IEnumerable<object>> itemsSelector = 
            _ => [];

        var processor = new FakeAgent("ProcessItem", "Processed");

        var workflow = Workflow.Create("Research")
            .ForEach("Process Documents", itemsSelector, processor)
            .Build();

        var loopNode = workflow.Nodes.OfType<LoopNode>().Single();

        loopNode.Name.Should().Be("Process Documents");
        loopNode.Items.Should().BeSameAs(itemsSelector);
        loopNode.Node.Should().BeSameAs(processor);
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Support_Chaining_Both_Overloads()
    {
        var workflow = Workflow.Create("Test")
            .ForEach(_ => [ "a", "b" ], new FakeAgent("A", "Done"))
            .ForEach("CustomLoop", _ => new[] { 1, 2 }.Select(x => (object)x), new FakeAgent("B", "Done"))
            .Build();

        workflow.Nodes.OfType<LoopNode>().Should().HaveCount(2);
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Items_Is_Null()
    {
        Action action = () => Workflow.Create("Test").ForEach(null!, new FakeAgent("A", "Done"));
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Node_Is_Null()
    {
        Action action = () => Workflow.Create("Test").ForEach(_ => [0], null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Name_Is_Empty()
    {
        Action action = () => Workflow.Create("Test").ForEach("", _ => [0], new FakeAgent("A", "Done"));
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowBuilder_ForEach_Should_Throw_When_Name_Is_Null()
    {
        Action action = () => Workflow.Create("Test").ForEach(null!, _ => [0], new FakeAgent("A", "Done"));
        action.Should().Throw<ArgumentException>();
    }
}
