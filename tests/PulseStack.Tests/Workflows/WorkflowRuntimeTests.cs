using FluentAssertions;
using PulseStack.Tests.Fakes;
using PulseStack.Abstractions.Agents;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Runtime.Diagnostics.Events;

using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowRuntimeTests
{
    [Fact]
    public async Task Workflow_Should_Execute_Node()
    {
        // Arrange

        var workflow =
            new WorkflowPipeline(
                "Workflow")
            .Add(
                new TestNode(
                    "Research"));

        var dispatcher = new RuntimeEventDispatcher();
        var runtime =
            new WorkflowRuntime(
                [
                    new FakeNodeExecutor()
                ], dispatcher);

        var context =
            new PipelineContext
            {
                Input = "Test",
                CurrentOutput = "Test"
            };

        // Act

        var result =
            await runtime.ExecuteAsync(
                workflow,
                context);

        // Assert

        result.Success.Should().BeTrue();

        result.Nodes.Should().ContainSingle();

        result.Nodes[0]
            .NodeName
            .Should()
            .Be("Research");

        result.FinalOutput
            .Should()
            .Be("Research");
    }

   [Fact]
   public async Task Workflow_Should_Execute_Nodes_In_Order()
   {
        // Arrange

        var executionOrder =
            new List<string>();

        var dispatcher = new RuntimeEventDispatcher();            

        var workflow =
            new WorkflowPipeline(
                "Workflow")
            .Add(
                new TestNode(
                    "First"))
            .Add(
                new TestNode(
                    "Second"))
            .Add(
                new TestNode(
                    "Third"));

        var runtime =
            new WorkflowRuntime(
                [
                    new FakeNodeExecutor(
                        executionOrder)
                ], dispatcher);

        var context =
            new PipelineContext
            {
                Input = "Test",
                CurrentOutput = "Test"
            };

        // Act

        await runtime.ExecuteAsync(
            workflow,
            context);

        // Assert

        executionOrder.Should().Equal(
            [
                "First",
                "Second",
                "Third"
            ]);
    }
    private sealed class TestNode
        : IPipelineNode
    {
        public TestNode(
            string name)
        {
            Name = name;
        }

        public string Name { get; }
    }

    [Fact]
    public async Task Workflow_Should_Throw_When_No_Executor_Exists()
    {
        var workflow =
            new WorkflowPipeline(
                "Workflow")
            .Add(
                new TestNode(
                    "Unknown"));

        var dispatcher = new RuntimeEventDispatcher();

        var runtime =
            new WorkflowRuntime([], dispatcher);

        var context =
            new PipelineContext();

        Func<Task> act =
            async () =>
                await runtime.ExecuteAsync(
                    workflow,
                    context);

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "*No executor registered*");
    }    
}