using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Pipelines;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowNodeExecutionTests
{
    [Fact]
    public async Task AgentNodeExecutor_Should_Execute_Agent()
    {
        // Arrange

        var executor = CreateExecutor();

        var agent =
            new FakeAgent(
                "Researcher",
                "Done");

        var context =
            new PipelineContext();

        // Act

        var result =
            await executor.ExecuteAsync(
                agent,
                context);

        // Assert

        result.Success.Should().BeTrue();

        result.NodeName.Should().Be("Researcher");

        result.Output.Should().Be("Done");
    }

    [Fact]
    public async Task Workflow_Should_Execute_Real_Agent_Node()
    {
        // Arrange

        var workflow =
            new WorkflowPipeline("Workflow")
                .Add(
                    new FakeAgent(
                        "Researcher",
                        "Research Complete"));

        var runtime =
            new WorkflowRuntime(
                [
                    CreateExecutor()
                ]);

        var context =
            new PipelineContext
            {
                Input = "AI orchestration"
            };

        // Act

        var result =
            await runtime.ExecuteAsync(
                workflow,
                context);

        // Assert

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().Be("Research Complete");

        result.Nodes.Should().ContainSingle();

        var node =
            result.Nodes.Single();

        node.NodeName.Should().Be("Researcher");

        node.Success.Should().BeTrue();

        node.Output.Should().Be("Research Complete");
    }

    [Fact]
    public async Task Workflow_Should_Execute_Pipeline_Node()
    {
        // Arrange

        var dispatcher =
            new RuntimeEventDispatcher();

        var pipeline =
            new SequentialPipeline(
                "Research Pipeline",
                dispatcher)
            .Add(
                new FakeAgent(
                    "Researcher",
                    "Research Complete"));

        var workflow =
            new WorkflowPipeline("Workflow")
                .Add(pipeline);

        var runtime =
            new WorkflowRuntime(
                [
                    new PipelineNodeExecutor()
                ]);

        var context =
            new PipelineContext
            {
                Input = "AI orchestration"
            };

        // Act

        var result =
            await runtime.ExecuteAsync(
                workflow,
                context);

        // Assert

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().Be(
            "Research Complete");

        result.Nodes.Should().ContainSingle();

        var node =
            result.Nodes.Single();

        node.NodeName.Should().Be(
            "Research Pipeline");

        node.Success.Should().BeTrue();
    }

    private static AgentNodeExecutor CreateExecutor()
    {
        var dispatcher =
            new RuntimeEventDispatcher();

        var runtime =
            new AgentRuntime(
                dispatcher);

        return new AgentNodeExecutor(
            runtime);
    }
}