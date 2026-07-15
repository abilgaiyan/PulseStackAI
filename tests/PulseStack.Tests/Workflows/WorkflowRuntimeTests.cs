using FluentAssertions;
using PulseStack.Tests.Fakes;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Workflows;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Abstractions.Common.Identity;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Agents.Runtime.Diagnostics;

using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowRuntimeTests
{
    [Fact]
    public async Task Workflow_Should_Execute_Step()
    {
        // Arrange

        var workflow =
            new Workflow(
                "Workflow")
            .Add(
                new TestWorkflowStep(
                    "Research"));

        var dispatcher = new RuntimeEventDispatcher();
        var runtime =
            new WorkflowRuntime(
                [
                    new FakeStepExecutor()
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

        result.Steps.Should().ContainSingle();

        result.Steps[0]
            .StepName
            .Should()
            .Be("Research");

        result.FinalOutput
            .Should()
            .Be("Research");
    }

   [Fact]
   public async Task Workflow_Should_Execute_Steps_In_Order()
   {
        // Arrange

        var executionOrder =
            new List<string>();

        var dispatcher = new RuntimeEventDispatcher();            

        var workflow =
            new Workflow(
                "Workflow")
            .Add(
                new TestWorkflowStep(
                    "First"))
            .Add(
                new TestWorkflowStep(
                    "Second"))
            .Add(
                new TestWorkflowStep(
                    "Third"));

        var runtime =
            new WorkflowRuntime(
                [
                    new FakeStepExecutor(
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
    private sealed class TestWorkflowStep
        : IWorkflowStep
    {
        public WorkflowStepId Id { get; } = WorkflowStepId.New();
        public TestWorkflowStep(
            string name)
        {
            Name = name;
        }

        public string Name { get; }

        public IReadOnlyList<IWorkflowStep> Children => [];
    }

    [Fact]
    public async Task Workflow_Should_Throw_When_No_Executor_Exists()
    {
        var workflow =
            new Workflow(
                "Workflow")
            .Add(
                new TestWorkflowStep(
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