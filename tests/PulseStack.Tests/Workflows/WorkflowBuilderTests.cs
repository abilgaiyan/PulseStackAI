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
}
