using FluentAssertions;
using PulseStack.Tests.Fakes;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Pipelines;
using PulseStack.Agents.Workflows;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime.Composition;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowNodeExecutionTests
{
    [Fact]
    public async Task DefaultNodeExecutor_Should_Execute_Agent()
    {
        var executor = new DefaultNodeExecutor();

        var agent = new FakeAgent("Researcher", "Done");
            

        var result = await executor.ExecuteAsync(agent, new PipelineContext());  

        result.Success.Should().BeTrue();
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
                    new DefaultNodeExecutor()
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

        var node = result.Nodes.Single();

        node.NodeName.Should().Be("Researcher");
        node.Success.Should().BeTrue();
        node.Output.Should().Be("Research Complete");
    }

}