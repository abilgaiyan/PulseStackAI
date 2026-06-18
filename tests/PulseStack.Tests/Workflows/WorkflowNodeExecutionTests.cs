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

    [Fact]
    public async Task WorkflowNodeExecutor_Should_Execute_Nested_Workflow()
    {
        var dispatcher =
            new RuntimeEventDispatcher();

        var agentRuntime =
            new AgentRuntime(
                dispatcher);

        var executors =
            new INodeExecutor[]
            {
                new AgentNodeExecutor(agentRuntime)
            };

        var workflowRuntime =
            new WorkflowRuntime(executors);

        var executor =
            new WorkflowNodeExecutor(
                workflowRuntime);

        var workflow =
            new WorkflowPipeline("Research")
                .Add(
                    new FakeAgent(
                        "Researcher",
                        "Research Complete"));

        var result =
            await executor.ExecuteAsync(
                workflow,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.Output.Should().Be(
            "Research Complete");
    }

   [Fact]
    public async Task Workflow_Should_Execute_Nested_Workflow()
    {
        // Arrange

        var dispatcher =
            new RuntimeEventDispatcher();

        var agentRuntime =
            new AgentRuntime(
                dispatcher);

        var nestedRuntime =
            new WorkflowRuntime(
            [
                new AgentNodeExecutor(agentRuntime)
            ]);

        var runtime =
            new WorkflowRuntime(
            [
                new AgentNodeExecutor(agentRuntime),
                new WorkflowNodeExecutor(nestedRuntime)
            ]);

        var childWorkflow =
            new WorkflowPipeline("Research")
                .Add(
                    new FakeAgent(
                        "Researcher",
                        "Research Complete"));

        var parentWorkflow =
            new WorkflowPipeline("Parent")
                .Add(childWorkflow);

        var context =
            new PipelineContext
            {
                Input = "AI orchestration"
            };

        // Act

        var result = await runtime.ExecuteAsync(parentWorkflow, context);

        // Assert

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().Be("Research Complete");

        result.Nodes.Should().ContainSingle();

        var node = result.Nodes.Single();

        node.NodeName.Should().Be("Research");

        node.Success.Should().BeTrue();

        node.Output.Should().Be("Research Complete");
    }

    [Fact]
    public async Task Workflow_Should_Execute_Multiple_Nested_Workflows()
    {
        // Arrange

        var dispatcher =
            new RuntimeEventDispatcher();

        var agentRuntime =
            new AgentRuntime(
                dispatcher);

        var nestedRuntime =
            new WorkflowRuntime(
            [
                new AgentNodeExecutor(agentRuntime)
            ]);

        var runtime =
            new WorkflowRuntime(
            [
                new AgentNodeExecutor(agentRuntime),
                new WorkflowNodeExecutor(nestedRuntime)
            ]);

        var researchWorkflow =
            new WorkflowPipeline("Research")
                .Add(
                    new FakeAgent(
                        "Researcher",
                        "Research Complete"));

        var summaryWorkflow =
            new WorkflowPipeline("Summary")
                .Add(
                    new FakeAgent(
                        "Summarizer",
                        "Summary Complete"));

        var parentWorkflow =
            new WorkflowPipeline("Parent")
                .Add(researchWorkflow)
                .Add(summaryWorkflow);

        var context =
            new PipelineContext
            {
                Input = "AI orchestration"
            };

        // Act

        var result =
            await runtime.ExecuteAsync(
                parentWorkflow,
                context);

        // Assert

        result.Success.Should().BeTrue();

        result.Nodes.Should().HaveCount(2);

        result.Nodes[0].NodeName.Should().Be("Research");

        result.Nodes[1].NodeName.Should().Be("Summary");

        result.Nodes.All(x => x.Success)
            .Should()
            .BeTrue();

        result.FinalOutput.Should().Be(
            "Summary Complete");
    }    

    [Fact]
    public async Task Workflow_Should_Preserve_Output_From_Nested_Workflow()
    {
        // Arrange

        var dispatcher =
            new RuntimeEventDispatcher();

        var agentRuntime =
            new AgentRuntime(
                dispatcher);

        var nestedRuntime =
            new WorkflowRuntime(
            [
                new AgentNodeExecutor(agentRuntime)
            ]);

        var runtime =
            new WorkflowRuntime(
            [
                new AgentNodeExecutor(agentRuntime),
                new WorkflowNodeExecutor(nestedRuntime)
            ]);

        var childWorkflow =
            new WorkflowPipeline("Research")
                .Add(
                    new FakeAgent(
                        "Researcher",
                        "Research Complete"));

        var parentWorkflow =
            new WorkflowPipeline("Parent")
                .Add(childWorkflow);

        var context =
            new PipelineContext
            {
                Input = "AI orchestration"
            };

        // Act

        var result =
            await runtime.ExecuteAsync(
                parentWorkflow,
                context);

        // Assert

        result.Success.Should().BeTrue();

        context.CurrentOutput.Should().Be(
            "Research Complete");

        result.FinalOutput.Should().Be(
            "Research Complete");

        result.Nodes.Should().ContainSingle();

        result.Nodes.Single().Output.Should().Be(
            "Research Complete");
    }

    [Fact]
    public async Task Workflow_Should_Pass_Output_Between_Nested_Workflows()
    {
        
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