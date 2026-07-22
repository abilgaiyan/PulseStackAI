using FluentAssertions;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows.Builders;

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
        workflow.Steps.Should().BeEmpty();
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

        var steps = workflow.Steps.OfType<RunStep>().ToList(); 
        steps.Should().ContainSingle();
        steps[0].Agent.Should().BeSameAs(agent);
    }

    [Fact]
    public void WorkflowBuilder_Should_Return_Workflow()
    {
        var workflow =
            Workflow.Create(
                    "Research")
                .Build();

        workflow.Should().BeOfType<Workflow>();
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

        workflow.Steps.Should().HaveCount(2);
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


}
