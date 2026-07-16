using FluentAssertions;
using PulseStack.Tests.Fakes;
using PulseStack.Abstractions.Workflows;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowTests
{
   [Fact]
    public void Workflow_Should_Accept_Agents()
    {
        var workflow =
            new Workflow(
                "TestWorkflow");

        var agent = 
            new FakeAgent(
                "Researcher",
                "Research"); 

        workflow.Add(agent);
            

        workflow.Steps
            .Should()
            .HaveCount(1);

        workflow.Steps
            .Single()
            .Should()
            .BeOfType<RunStep>();            

        ((RunStep)workflow.Steps.Single())
            .Agent
            .Should()
            .BeSameAs(agent);
    }

    [Fact]
    public void Workflow_Should_Preserve_Step_Order()
    {
        var workflow =
            new Workflow(
                "Workflow");

        var first =
            new FakeAgent(
                "First",
                "A");

        var second =
            new FakeAgent(
                "Second",
                "B");

        workflow
            .Add(first)
            .Add(second);

        var steps = workflow.Steps.OfType<RunStep>().ToList();    

        steps[0].Agent.Should().Be(first);

        steps[1].Agent.Should().Be(second);
    }

}
