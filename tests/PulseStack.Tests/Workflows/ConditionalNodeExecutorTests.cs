using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflow.Nodes;
using PulseStack.Abstractions.Workflow.Conditions;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;


public class ConditionalNodeExecutorTest
{
    
     [Fact]
    public async Task ConditionalNode_Should_Execute_When_Condition_Is_True()
    {
        var executors =
            new List<INodeExecutor>();

        var resolver =
            new NodeExecutorResolver(
                executors);

        executors.Add(
            WorkflowRuntimeFactory.CreateAgentExecutor());

        var executor =
            new ConditionalNodeExecutor(
                resolver);

        var node =
            new ConditionalNode(
                "Condition",
                new DelegateCondition(_ => true),
                new FakeAgent(
                    "Researcher",
                    "Executed"));

        var result =
            await executor.ExecuteAsync(
                node,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.NodeName.Should().Be("Condition");

        result.Output.Should().Be("Executed");
    }

    [Fact]
    public async Task ConditionalNode_Should_Return_Own_Name_And_Preserve_Child_Result()
    {
        var childUsage =
            new AIUsage
            {
                Provider = "Test",
                Model = "model",
                PromptTokens = 3,
                CompletionTokens = 4
            };

        var executors =
            new List<INodeExecutor>();

        var resolver =
            new NodeExecutorResolver(
                executors);

        executors.Add(
            new FakeNodeExecutor(
                success: false,
                output: "Child Output",
                usage: childUsage));

        var executor =
            new ConditionalNodeExecutor(
                resolver);

        var node =
            new ConditionalNode(
                "Condition",
                new DelegateCondition(_ => true),
                new FakeAgent(
                    "Child",
                    "Ignored"));

        var result =
            await executor.ExecuteAsync(
                node,
                new PipelineContext());

        result.NodeName.Should().Be("Condition");
        result.Success.Should().BeFalse();
        result.Output.Should().Be("Child Output");
        result.Usage.Should().BeSameAs(childUsage);
    }

    [Fact]
    public async Task ConditionalNode_Should_Skip_When_Condition_Is_False()
    {
        var executors =
            new List<INodeExecutor>();

        var resolver =
            new NodeExecutorResolver(
                executors);

        executors.Add(
            WorkflowRuntimeFactory.CreateAgentExecutor());

        var executor =
            new ConditionalNodeExecutor(
                resolver);

        var node =
            new ConditionalNode(
                "Condition",
                new DelegateCondition(_ => false),
                new FakeAgent(
                    "Researcher",
                    "Executed"));

        var result =
            await executor.ExecuteAsync(
                node,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.Output.Should().BeNull();
    }

    [Fact]
    public async Task Workflow_Should_Execute_Conditional_Node()
    {
        var runtime =
            WorkflowRuntimeFactory
                .CreateWithNestedWorkflowSupport();

        var workflow =
            new WorkflowDefinition("Workflow")
                .Add(
                    new ConditionalNode(
                        "Condition",
                        new DelegateCondition(_ => true),
                        new FakeAgent(
                            "Researcher",
                            "Executed")));

        var result =
            await runtime.ExecuteAsync(
                workflow,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().Be("Executed");
    }

    [Fact]
    public async Task Workflow_Should_Skip_Conditional_Node()
    {
        var runtime =
            WorkflowRuntimeFactory
                .CreateWithNestedWorkflowSupport();

        var workflow =
            new WorkflowDefinition("Workflow")
                .Add(
                    new ConditionalNode(
                        "Condition",
                        new DelegateCondition(_ => false),
                        new FakeAgent(
                            "Researcher",
                            "Executed")));

        var result =
            await runtime.ExecuteAsync(
                workflow,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().BeEmpty();
    }

    [Fact]
    public async Task ConditionalNode_Should_Preserve_Context_When_Skipped()
    {
        var runtime =
            WorkflowRuntimeFactory
                .CreateWithNestedWorkflowSupport();

        var workflow =
            new WorkflowDefinition("Workflow")
                .Add(
                    new FakeAgent(
                        "Step1",
                        "Research Complete"))
                .Add(
                    new ConditionalNode(
                        "Condition",
                        new DelegateCondition(_ => false),
                        new FakeAgent(
                            "Step2",
                            "Should Not Execute")));

        var result =
            await runtime.ExecuteAsync(
                workflow,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().Be(
            "Research Complete");
    }

}
