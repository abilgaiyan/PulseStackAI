using FluentAssertions;
using PulseStack.Tests.Fakes;
using PulseStack.Abstractions.Workflow.Nodes;
using PulseStack.Agents.Pipelines;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowDefinitionTests
{
   [Fact]
    public void Workflow_Should_Accept_Agents()
    {
        var workflow =
            new WorkflowDefinition(
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
            new WorkflowDefinition(
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
            new WorkflowDefinition(
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
