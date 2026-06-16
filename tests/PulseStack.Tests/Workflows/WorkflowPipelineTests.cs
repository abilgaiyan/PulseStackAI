using FluentAssertions;
using PulseStack.Tests.Fakes;
using PulseStack.Agents.Workflows;
using PulseStack.Agents.Pipelines;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowPipelineTests
{
   [Fact]
    public void Workflow_Should_Accept_Agents()
    {
        var workflow =
            new WorkflowPipeline(
                "TestWorkflow");

        workflow.Add(
            new FakeAgent(
                "Researcher",
                "Research"));

        workflow.Nodes
            .Should()
            .HaveCount(1);
    }

    [Fact]
    public void Workflow_Should_Accept_Pipelines()
    {
        var workflow =
            new WorkflowPipeline(
                "TestWorkflow");

        var pipeline =
            new SequentialPipeline(
                "ResearchPipeline");

        workflow.Add(
            pipeline);

        workflow.Nodes
            .Should()
            .ContainSingle();
    }

    [Fact]
    public void Workflow_Should_Preserve_Node_Order()
    {
        var workflow =
            new WorkflowPipeline(
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

        workflow.Nodes[0]
            .Should()
            .Be(first);

        workflow.Nodes[1]
            .Should()
            .Be(second);
    }

}
