using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class LoopNodeExecutorTests
{
    [Fact]
    public async Task LoopNode_Should_Execute_For_Each_Item()
    {
        var runtime =
            WorkflowRuntimeFactory.Create();

        var workflow =
            new WorkflowPipeline("Workflow")
                .Add(
                    new LoopNode(
                        "Loop",
                        _ => new object[]
                        {
                            "A",
                            "B",
                            "C"
                        },
                        new FakeAgent(
                            "Researcher",
                            "Executed")));

        var context = new PipelineContext();

        var result =
            await runtime.ExecuteAsync(
                workflow,
                context);

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().Be("Executed");

        // Verify loop executed for all items
        context.Items["CurrentItem"].Should().Be("C"); // Last item
    
        result.Nodes.Should().ContainSingle();
        var loopNode = result.Nodes.Single();
        loopNode.NodeName.Should().Be("Loop");
        loopNode.Success.Should().BeTrue();
    }

    [Fact]
    public async Task LoopNode_Should_Pass_Current_Item()
    {
        var runtime =
            WorkflowRuntimeFactory.Create();

        var workflow =
            new WorkflowPipeline("Workflow")
                .Add(
                    new LoopNode(
                        "Loop",
                        _ => new object[]
                        {
                            "Doc1"
                        },
                        new LoopAwareFakeAgent(
                            "Researcher")));

        var context =
            new PipelineContext();

        var result =
            await runtime.ExecuteAsync(
                workflow,
                context);

        result.Success.Should().BeTrue();

        context.Items["CurrentItem"].Should().Be("Doc1");
    
        // The agent's output should reflect processing
        result.FinalOutput.Should().Be("Received: Doc1");
        context.CurrentOutput.Should().Be("Received: Doc1");
    }

    [Fact]
    public async Task LoopNode_Should_Stop_On_Failure()
    {
        var executors =
            new List<INodeExecutor>();

        var alwaysFailExecutor =
            new AlwaysFailNodeExecutor();

        executors.Add(alwaysFailExecutor);

        var resolver =
            new NodeExecutorResolver(executors);

        var loopExecutor =
            new LoopNodeExecutor(resolver);

        var node =
            new LoopNode(
                "Loop",
                _ => new object[]
                {
                    "A",
                    "B",
                    "C"
                },
                new FakeAgent(
                    "Researcher",
                    "Executed"));    
            

        var result =
            await loopExecutor.ExecuteAsync(
                node,
                new PipelineContext());

        result.Success.Should().BeFalse();
        result.NodeName.Should().Be("Loop");
        alwaysFailExecutor.Attempts.Should().Be(1);

    }

    [Fact]
    public async Task LoopNode_Should_Return_Success_When_Empty()
    {
       var runtime =
            WorkflowRuntimeFactory.Create();

        var workflow =
            new WorkflowPipeline("Workflow")
                .Add(
                    new LoopNode(
                        "Loop",
                        _ => [],
                        new LoopAwareFakeAgent(
                            "Researcher")));

        var context =
            new PipelineContext();

        var result =
            await runtime.ExecuteAsync(
                workflow,
                context);

        result.Success.Should().BeTrue();
        
        result.Nodes.Should().ContainSingle();

        var loopResult =
            result.Nodes.Single();

        loopResult.NodeName.Should().Be("Loop");

        loopResult.Success.Should().BeTrue();

        result.FinalOutput.Should().BeEmpty();

        context.CurrentOutput.Should().BeNullOrEmpty();

    }

    [Fact]
    public async Task Workflow_Should_Execute_Loop_Node()
    {
        var runtime =
            WorkflowRuntimeFactory.Create();

        var workflow =
            new WorkflowPipeline("Workflow")
                .Add(
                    new LoopNode(
                        "Loop",
                        _ => new object[]
                        {
                            "Item1",
                            "Item2"
                        },
                        new LoopAwareFakeAgent(
                            "Researcher")));

        var context =
            new PipelineContext();

        var result =
            await runtime.ExecuteAsync(
                workflow,
                context);

        result.Success.Should().BeTrue();

        result.Nodes.Should().ContainSingle();

        result.Nodes[0].NodeName.Should().Be("Loop");

        context.Items["CurrentItem"]
            .Should().Be("Item2");

        result.FinalOutput
            .Should().Be("Received: Item2");
    }

    [Fact]
    public async Task Workflow_Should_Execute_Nested_Loop_Node()
    {
          // Arrange
    
        var runtime = WorkflowRuntimeFactory.CreateWithNestedWorkflowSupport();
        
        // Create a nested workflow
        var nestedWorkflow = new WorkflowPipeline("ProcessItem")
            .Add(new FakeAgent("Step1", "Step1 Done"))
            .Add(new LoopAwareFakeAgent("Step2"));
        
        var workflow = new WorkflowPipeline("Main")
            .Add(
                new LoopNode(
                    "Loop",
                    _ => new object[] { "Item1", "Item2" },
                    nestedWorkflow));
        
        var context = new PipelineContext();
        
        // Act
        
        var result = await runtime.ExecuteAsync(workflow, context);
        
        // Assert
        
        result.Success.Should().BeTrue();
        
        // The last item was processed through the nested workflow
        context.Items["CurrentItem"].Should().Be("Item2");
        
        // The nested workflow's final output should be the result
        result.FinalOutput.Should().Be("Received: Item2");
    }
}