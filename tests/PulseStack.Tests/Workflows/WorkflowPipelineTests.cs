using FluentAssertions;
using PulseStack.Tests.Fakes;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Agents.Pipelines;
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

        workflow.Steps[0]
            .Should()
            .Be(first);

        workflow.Steps[1]
            .Should()
            .Be(second);
    }

}
