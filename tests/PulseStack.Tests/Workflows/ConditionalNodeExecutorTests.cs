using FluentAssertions;
using PulseStack.Abstractions.Agents;
using PulseStack.Abstractions.Runtime.Pipeline;
using PulseStack.Abstractions.Workflows;
using PulseStack.Abstractions.Workflows.Steps;
using PulseStack.Abstractions.Workflows.Conditions;
using PulseStack.Abstractions.Runtime.Usage;
using PulseStack.Agents.Runtime.Composition;
using PulseStack.Tests.Fakes;
using Xunit;

namespace PulseStack.Tests.Workflows;


public class ConditionalStepExecutorTest
{
    
     [Fact]
    public async Task ConditionalStep_Should_Execute_When_Condition_Is_True()
    {
        var executors =
            new List<IStepExecutor>();

        var resolver =
            new StepExecutorResolver(
                executors);

        executors.Add(
            WorkflowRuntimeFactory.CreateAgentExecutor());

        var executor =
            new ConditionalStepExecutor(
                resolver);

        var agent = 
            new FakeAgent(
                "Researcher",
                "Executed");       

        var step =
            new ConditionalStep(
                "Condition",
                new DelegateCondition(_ => true),
                new RunStep(agent));

        var result =
            await executor.ExecuteAsync(
                step,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.StepName.Should().Be("Condition");

        result.Output.Should().Be("Executed");
    }

    [Fact]
    public async Task ConditionalStep_Should_Return_Own_Name_And_Preserve_Child_Result()
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
            new List<IStepExecutor>();

        var resolver =
            new StepExecutorResolver(
                executors);

        executors.Add(
            new FakeStepExecutor(
                success: false,
                output: "Child Output",
                usage: childUsage));

        var executor =
            new ConditionalStepExecutor(
                resolver);

        var agent = 
            new FakeAgent(
                "Child",
                "Ignored");        

        var step =
            new ConditionalStep(
                "Condition",
                new DelegateCondition(_ => true),
                new RunStep(agent));

        var result =
            await executor.ExecuteAsync(
                step,
                new PipelineContext());

        result.StepName.Should().Be("Condition");
        result.Success.Should().BeFalse();
        result.Output.Should().Be("Child Output");
        result.Usage.Should().BeSameAs(childUsage);
    }

    [Fact]
    public async Task ConditionalStep_Should_Skip_When_Condition_Is_False()
    {
        var executors =
            new List<IStepExecutor>();

        var resolver =
            new StepExecutorResolver(
                executors);

        executors.Add(
            WorkflowRuntimeFactory.CreateAgentExecutor());

        var executor =
            new ConditionalStepExecutor(
                resolver);

        var agent =                 
            new FakeAgent(
                "Researcher",
                "Executed");        

        var step =
            new ConditionalStep(
                "Condition",
                new DelegateCondition(_ => false),
                new RunStep(agent));

        var result =
            await executor.ExecuteAsync(
                step,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.Output.Should().BeNull();
    }

    [Fact]
    public async Task Workflow_Should_Execute_Conditional_Step()
    {
        var runtime =
            WorkflowRuntimeFactory
                .CreateWithNestedWorkflowSupport();

        var agent = 
            new FakeAgent(
                "Researcher",
                "Executed");

        var workflow =
            new Workflow("Workflow")
                .Add(
                    new ConditionalStep(
                        "Condition",
                        new DelegateCondition(_ => true),
                    new RunStep(agent)));

        var result =
            await runtime.ExecuteAsync(
                workflow,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().Be("Executed");
    }

    [Fact]
    public async Task Workflow_Should_Skip_Conditional_Step()
    {
        var runtime =
            WorkflowRuntimeFactory
                .CreateWithNestedWorkflowSupport();

        var agent = 
            new FakeAgent(
                "Researcher",
                "Executed");       

        var workflow =
            new Workflow("Workflow")
                .Add(
                    new ConditionalStep(
                        "Condition",
                        new DelegateCondition(_ => false),
                        new RunStep(agent)
                    ));

        var result =
            await runtime.ExecuteAsync(
                workflow,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().BeEmpty();
    }

    [Fact]
    public async Task ConditionalStep_Should_Preserve_Context_When_Skipped()
    {
        var runtime =
            WorkflowRuntimeFactory
                .CreateWithNestedWorkflowSupport();

        var workflow =
            new Workflow("Workflow")
                .Add(
                    new FakeAgent(
                        "Step1",
                        "Research Complete"))
                .Add(
                    new ConditionalStep(
                        "Condition",
                        new DelegateCondition(_ => false),
                        new RunStep(new FakeAgent(
                            "Step2",
                            "Should Not Execute"))));

        var result =
            await runtime.ExecuteAsync(
                workflow,
                new PipelineContext());

        result.Success.Should().BeTrue();

        result.FinalOutput.Should().Be(
            "Research Complete");
    }
}
