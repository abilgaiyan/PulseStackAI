using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Agents.Runtime.Diagnostics;
using PulseStack.Agents.Pipelines;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class WorkflowStepExecutionTests
{
    [Fact]
    public async Task AgentStepExecutor_Should_Execute_Agent()
    {
        // Arrange

        var executor = WorkflowTestRuntimeFactory.CreateAgentExecutor();

        var workflowStep =
            new RunStep(new FakeAgent(
                "Researcher",
                "Done"));

        var context =
            new PipelineContext();

        // Act

        var result =
            await executor.ExecuteAsync(
                workflowStep,
                context);

        // Assert

        result.Success.Should().BeTrue();

        result.StepName.Should().Be("Researcher");

        result.Output.Should().Be("Done");
    }

    [Fact]
    public async Task Workflow_Should_Execute_Real_Agent_Step()
    {
        // Arrange

        var workflow =
            new Workflow("Workflow")
                .Add(
                    new FakeAgent(
                        "Researcher",
                        "Research Complete"));

        var runtime = WorkflowTestRuntimeFactory.Create();

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

        result.Steps.Should().ContainSingle();

        var step =
            result.Steps.Single();

        step.StepName.Should().Be("Researcher");

        step.Success.Should().BeTrue();

        step.Output.Should().Be("Research Complete");
    }

    [Fact]
    public async Task Workflow_Should_Execute_RunStep()
    {
        // Arrange

        var dispatcher =
            new RuntimeEventDispatcher();

        var workflow =
        new Workflow("Workflow")
            .Add(
                new FakeAgent(
                    "Researcher",
                    "Research Complete"));

        
        var runtime = WorkflowTestRuntimeFactory.Create();
        
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

        result.Steps.Should().ContainSingle();

        var step =
            result.Steps.Single();

        step.StepName.Should().Be(
            "Researcher");

        step.Success.Should().BeTrue();
    }

    [Fact]
    public async Task WorkflowStepExecutor_Should_Execute_Nested_Workflow()
    {
        var runtime =
            WorkflowTestRuntimeFactory.Create();

        var executor =
            new WorkflowStepExecutor(
                new Lazy<IWorkflowRuntime>(
                    () => runtime));

        var workflow =
            new Workflow("Research")
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
    public async Task WorkflowStepExecutor_Should_Return_Own_Name_And_Preserve_Child_Usage()
    {
        var usage =
            new AIUsage
            {
                Provider = "Test",
                Model = "model",
                PromptTokens = 11,
                CompletionTokens = 13
            };

        var dispatcher =
            new RuntimeEventDispatcher();

        var runtime =
            new WorkflowRuntime(
                [
                    new FakeStepExecutor(
                        output: "Nested Output",
                        usage: usage)
                ],
                dispatcher);

        var executor =
            new WorkflowStepExecutor(
                new Lazy<IWorkflowRuntime>(
                    () => runtime));

        var workflow =
            new Workflow("Nested")
                .Add(
                    new FakeAgent(
                        "Child",
                        "Ignored"));

        var result =
            await executor.ExecuteAsync(
                workflow,
                new PipelineContext());

        result.StepName.Should().Be("Nested");
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Nested Output");
        result.Usage.Should().NotBeNull();
        result.Usage!.PromptTokens.Should().Be(11);
        result.Usage.CompletionTokens.Should().Be(13);
    }

   [Fact]
    public async Task Workflow_Should_Execute_Nested_Workflow()
    {
        // Arrange

      var runtime =
            WorkflowTestRuntimeFactory
                .CreateWithNestedWorkflowSupport();

        var childWorkflow =
            new Workflow("Research")
                .Add(
                    new FakeAgent(
                        "Researcher",
                        "Research Complete"));

        var parentWorkflow =
            new Workflow("Parent")
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

        result.Steps.Should().ContainSingle();

        var step = result.Steps.Single();

        step.StepName.Should().Be("Research");

        step.Success.Should().BeTrue();

        step.Output.Should().Be("Research Complete");
    }

    [Fact]
    public async Task Workflow_Should_Execute_Multiple_Nested_Workflows()
    {
        // Arrange

      var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var researchWorkflow =
            new Workflow("Research")
                .Add(
                    new FakeAgent(
                        "Researcher",
                        "Research Complete"));

        var summaryWorkflow =
            new Workflow("Summary")
                .Add(
                    new FakeAgent(
                        "Summarizer",
                        "Summary Complete"));

        var parentWorkflow =
            new Workflow("Parent")
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

        result.Steps.Should().HaveCount(2);

        result.Steps[0].StepName.Should().Be("Research");

        result.Steps[1].StepName.Should().Be("Summary");

        result.Steps.All(x => x.Success)
            .Should()
            .BeTrue();

        result.FinalOutput.Should().Be(
            "Summary Complete");
    }    

    [Fact]
    public async Task Workflow_Should_Preserve_Output_From_Nested_Workflow()
    {
        // Arrange

        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();

        var childWorkflow =
            new Workflow("Research")
                .Add(
                    new FakeAgent(
                        "Researcher",
                        "Research Complete"));

        var parentWorkflow =
            new Workflow("Parent")
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

        result.Steps.Should().ContainSingle();

        result.Steps.Single().Output.Should().Be(
            "Research Complete");
    }

    [Fact]
    public async Task Workflow_Should_Pass_Output_Between_Nested_Workflows()
    {
        // Arrange
        
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();
        
        // Create first workflow that produces output
        var firstWorkflow = new Workflow("FirstWorkflow")
            .Add(new FakeAgent("Agent1", "Initial Result"));
        
        // Create second workflow that consumes output from first
        var secondWorkflow = new Workflow("SecondWorkflow")
            .Add(new ContextAwareFakeAgent("Agent2"));
        
        // Create parent workflow that chains them together
        var parentWorkflow = new Workflow("Parent")
            .Add(firstWorkflow)
            .Add(secondWorkflow);
        
        var context = new PipelineContext
        {
            Input = "Initial Context Input"
        };
        
        // Act
        
        var result = await runtime.ExecuteAsync(parentWorkflow, context);
        
        // Assert
        
        result.Success.Should().BeTrue();
        
        // Verify output flows between workflows
        result.Steps.Should().HaveCount(2);
        
        // First workflow output should be available as input to second
        var firstStep = result.Steps[0];
        firstStep.StepName.Should().Be("FirstWorkflow");
        firstStep.Success.Should().BeTrue();
        firstStep.Output.Should().Be("Initial Result");
        
        // Second workflow should process the output from first
        var secondStep = result.Steps[1];
        secondStep.StepName.Should().Be("SecondWorkflow");
        secondStep.Success.Should().BeTrue();
        secondStep.Output.Should().Be("Received: Initial Result");
        
        // Final output should reflect the chained processing
        result.FinalOutput.Should().Be("Received: Initial Result");
        
        // Context should maintain the latest output
        context.CurrentOutput.Should().Be("Received: Initial Result");
    }

 }
