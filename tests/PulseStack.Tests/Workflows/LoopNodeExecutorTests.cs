using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;

public class LoopStepExecutorTests
{
    [Fact]
    public async Task LoopStep_Should_Execute_For_Each_Item()
    {
        var runtime =
            WorkflowTestRuntimeFactory.Create();

        var workflow =
            new Workflow("Workflow")
                .Add(
                    new LoopStep(
                        "Loop",
                        _ => new object[]
                        {
                            "A",
                            "B",
                            "C"
                        },
                        new RunStep(new FakeAgent(
                            "Researcher",
                            "Executed"))));

        var context = new PipelineContext();

        var result =
            await runtime.ExecuteAsync(
                workflow,
                context);

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().Be("Executed");

        // Verify loop executed for all items
        context.Items["CurrentItem"].Should().Be("C"); // Last item
    
        result.Steps.Should().ContainSingle();
        var loopStep = result.Steps.Single();
        loopStep.StepName.Should().Be("Loop");
        loopStep.Success.Should().BeTrue();
    }

    [Fact]
    public async Task LoopStep_Should_Pass_Current_Item()
    {
        var runtime =
            WorkflowTestRuntimeFactory.Create();

        var workflow =
            new Workflow("Workflow")
                .Add(
                    new LoopStep(
                        "Loop",
                        _ => new object[]
                        {
                            "Doc1"
                        },
                        new RunStep(new LoopAwareFakeAgent(
                            "Researcher"))));

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
    public async Task LoopStep_Should_Stop_On_Failure()
    {
        var executors =
            new List<IStepExecutor>();

        var alwaysFailExecutor =
            new AlwaysFailStepExecutor();

        executors.Add(alwaysFailExecutor);

        var resolver =
            new StepExecutorResolver(executors);

        var loopExecutor =
            new LoopStepExecutor(resolver);

        var step =
            new LoopStep(
                "Loop",
                _ => new object[]
                {
                    "A",
                    "B",
                    "C"
                },
                new RunStep(new FakeAgent(
                    "Researcher",
                    "Executed")));    
            

        var result =
            await loopExecutor.ExecuteAsync(
                step,
                new PipelineContext());

        result.Success.Should().BeFalse();
        result.StepName.Should().Be("Loop");
        alwaysFailExecutor.Attempts.Should().Be(1);

    }

    [Fact]
    public async Task LoopStep_Should_Return_Own_Name_And_Preserve_Failed_Child_Result()
    {
        var usage =
            new AIUsage
            {
                Provider = "Test",
                Model = "model",
                PromptTokens = 2,
                CompletionTokens = 3
            };

        var executors =
            new List<IStepExecutor>
            {
                new FakeStepExecutor(
                    success: false,
                    output: "Failed Child",
                    usage: usage)
            };

        var resolver =
            new StepExecutorResolver(
                executors);

        var loopExecutor =
            new LoopStepExecutor(
                resolver);

        var step =
            new LoopStep(
                "Loop",
                _ => new object[]
                {
                    "A"
                },
                new RunStep(new FakeAgent(
                    "Researcher",
                    "Executed")));

        var result =
            await loopExecutor.ExecuteAsync(
                step,
                new PipelineContext());

        result.StepName.Should().Be("Loop");
        result.Success.Should().BeFalse();
        result.Output.Should().Be("Failed Child");
        result.Usage.Should().BeSameAs(usage);
    }

    [Fact]
    public async Task LoopStep_Should_Return_Success_When_Empty()
    {
       var runtime =
            WorkflowTestRuntimeFactory.Create();

        var workflow =
            new Workflow("Workflow")
                .Add(
                    new LoopStep(
                        "Loop",
                        _ => [],
                        new RunStep(new LoopAwareFakeAgent(
                            "Researcher"))));

        var context =
            new PipelineContext();

        var result =
            await runtime.ExecuteAsync(
                workflow,
                context);

        result.Success.Should().BeTrue();
        
        result.Steps.Should().ContainSingle();

        var loopResult =
            result.Steps.Single();

        loopResult.StepName.Should().Be("Loop");

        loopResult.Success.Should().BeTrue();

        result.FinalOutput.Should().BeEmpty();

        context.CurrentOutput.Should().BeNullOrEmpty();

    }

    [Fact]
    public async Task Workflow_Should_Execute_Loop_Step()
    {
        var runtime =
            WorkflowTestRuntimeFactory.Create();

        var workflow =
            new Workflow("Workflow")
                .Add(
                    new LoopStep(
                        "Loop",
                        _ => new object[]
                        {
                            "Item1",
                            "Item2"
                        },
                        new RunStep(new LoopAwareFakeAgent(
                            "Researcher"))));

        var context =
            new PipelineContext();

        var result =
            await runtime.ExecuteAsync(
                workflow,
                context);

        result.Success.Should().BeTrue();

        result.Steps.Should().ContainSingle();

        result.Steps[0].StepName.Should().Be("Loop");

        context.Items["CurrentItem"]
            .Should().Be("Item2");

        result.FinalOutput
            .Should().Be("Received: Item2");
    }

    [Fact]
    public async Task Workflow_Should_Execute_Nested_Loop_Step()
    {
          // Arrange
    
        var runtime = WorkflowTestRuntimeFactory.CreateWithNestedWorkflowSupport();
        
        // Create a nested workflow
        var nestedWorkflow = new Workflow("ProcessItem")
            .Add(new FakeAgent("Step1", "Step1 Done"))
            .Add(new LoopAwareFakeAgent("Step2"));
        
        var workflow = new Workflow("Main")
            .Add(
                new LoopStep(
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
