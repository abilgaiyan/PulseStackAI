using FluentAssertions;
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
}
