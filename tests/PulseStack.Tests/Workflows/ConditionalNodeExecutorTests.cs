using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;


public class ConditionalNodeExecutorTest
{
    
     [Fact]
    public async Task ConditionalNode_Should_Execute_When_Condition_Is_True()
    {
        var node =
            new ConditionalNode(
                "Condition",
                new DelegateCondition(_ => true),
                new FakeAgent(
                    "Researcher",
                    "Executed"));

        // executor comes later
        // var executor = ...

        // var result =
        //     await executor.ExecuteAsync(
        //         node,
        //         new PipelineContext());

        // result.Success.Should().BeTrue();
        // result.Output.Should().Be("Executed");
    }

   [Fact]
    public async Task ConditionalNode_Should_Skip_When_Condition_Is_False()
    {
        var node =
            new ConditionalNode(
                "Condition",
                new DelegateCondition(_ => false),
                new FakeAgent(
                    "Researcher",
                    "Executed"));

        // executor comes later
    }

    [Fact]
    public async Task Workflow_Should_Execute_Conditional_Node()
    {
        var runtime =
            WorkflowRuntimeFactory
                .CreateWithNestedWorkflowSupport();

        var workflow =
            new WorkflowPipeline("Workflow")
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
            new WorkflowPipeline("Workflow")
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
            new WorkflowPipeline("Workflow")
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
