using Xunit;
using FluentAssertions;
using PulseStack.Abstractions.Workflows;


namespace PulseStack.Tests.Workflows.Builders;
public class WorkflowBuilderNestedWorkflowTests
{
        [Fact]
    public void WorkflowBuilder_Should_Add_Nested_Workflow()
    {
        var childWorkflow =
            new Workflow(
                "Child");

        var workflow =
                Workflow.Create("Parent")
                    .Workflow(childWorkflow)
                .Build();

        workflow.Steps.Should().ContainSingle();
        workflow.Steps.Single().Should().BeSameAs(childWorkflow);
    }

}